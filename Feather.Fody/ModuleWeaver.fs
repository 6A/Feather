namespace Feather

open System.Collections.Generic
open System.Linq

open Fody
open Mono.Cecil
open Mono.Cecil.Cil

open BodyWeaving
open TypeReplacing


/// Weaver that will purge all FSharp.Core-related members from the visited module.
type ModuleWeaver() as this =
    inherit BaseModuleWeaver()

    let logDebug str   = Printf.ksprintf this.LogDebug.Invoke str
    let logWarning str = Printf.ksprintf this.LogWarning.Invoke str
    let logError str   = Printf.ksprintf this.LogError.Invoke str

    // ====================================================================================
    // ==== FIELDS AND PROPERTIES =========================================================
    // ====================================================================================

    /// List of all attributes to remove if encountered.
    static let attributesToRemove = [|
        "AutoOpenAttribute";
        "CompilationMappingAttribute";
        "CompilationArgumentsCountsAttribute";
        "SealedAttribute";
        "AbstractClassAttribute";
        "StructAttribute"
    |]

    override __.GetAssembliesForScanning() = Seq.empty
    override __.ShouldCleanReference = true


    // ====================================================================================
    // ==== CLEANING ======================================================================
    // ====================================================================================

    override __.Execute() =
        // 1. Find and remove reference to FSharp.Core
        this.ModuleDefinition.AssemblyReferences
                             .Remove(fun asm -> asm.Name = "FSharp.Core")
        this.ModuleDefinition.ModuleReferences
                             .Remove(fun modl -> modl.Name = "FSharp.Core")
        
        // 2. Remove F# metadata
        this.ModuleDefinition.CustomAttributes
                             .Remove(fun attr -> attr.AttributeType.IsFSharp)
        this.ModuleDefinition.Assembly.CustomAttributes
                                      .Remove(fun attr -> attr.AttributeType.IsFSharp)

        // 3. Purge all C# references from types
        for i, typ in this.ModuleDefinition.Types.GetMutableEnumerator() do
            if this.PurgeType(typ) then
                this.ModuleDefinition.Types.RemoveAt(i)

    member __.PurgeType(ty: TypeDefinition) =
        // 1. Clean attributes
        for i, attr in ty.CustomAttributes.GetMutableEnumerator() do
            if attr.AttributeType.IsFSharp then
                if attributesToRemove.Contains(attr.AttributeType.Name) then
                    ty.CustomAttributes.RemoveAt(i)

        // 2. Clean properties
        for i, prop in ty.Properties.GetMutableEnumerator() do
            if this.PurgeProperty(prop) then
                ty.Properties.RemoveAt(i)
        
        // 3. Clean fields
        for i, field in ty.Fields.GetMutableEnumerator() do
            if this.PurgeField(field) then
                ty.Fields.RemoveAt(i)
        
        // 4. Clean events
        for i, event in ty.Events.GetMutableEnumerator() do
            if this.PurgeEvent(event) then
                ty.Events.RemoveAt(i)
        
        // 5. Clean methods
        for i, method in ty.Methods.GetMutableEnumerator() do
            if this.PurgeMethod(method) then
                ty.Methods.RemoveAt(i)

        // 6. Clean inner types
        for i, typ in ty.NestedTypes.GetMutableEnumerator() do
            if this.PurgeType(typ) then
                ty.NestedTypes.RemoveAt(i)
        
        let remove = not (isNull ty.DeclaringType) && ty.DeclaringType.IsFSharp
                  || ty.Interfaces.Any(fun x -> x.InterfaceType.IsFSharp)

        if remove then
            logDebug "Type %s inherits or implements an FSharp.Core type and will be removed." ty.FullName
        
        remove
    
    member __.PurgeField(field: FieldDefinition) =
        // 1. Clean attributes
        for i, attr in field.CustomAttributes.GetMutableEnumerator() do
            if attr.AttributeType.IsFSharp then
                field.CustomAttributes.RemoveAt(i)
        
        false

    member __.PurgeEvent(event: EventDefinition) =
        // 1. Clean attributes
        for i, attr in event.CustomAttributes.GetMutableEnumerator() do
            if attr.AttributeType.IsFSharp then
                event.CustomAttributes.RemoveAt(i)
        
        false

    member __.PurgeProperty(prop: PropertyDefinition) =
        // 1. Clean attributes
        for i, attr in prop.CustomAttributes.GetMutableEnumerator() do
            if attr.AttributeType.IsFSharp then
                prop.CustomAttributes.RemoveAt(i)
        
        // 2. Replace getter, setter
        if prop.GetMethod |> (not << isNull) && this.PurgeMethod(prop.GetMethod) then
            prop.GetMethod <- null
        
        if prop.SetMethod |> (not << isNull) && this.PurgeMethod(prop.SetMethod) then
            prop.SetMethod <- null
        
        false


    // ====================================================================================
    // ==== CLEANING METHODS ==============================================================
    // ====================================================================================

    member __.ReplaceParameter(param: ParameterDefinition) =
        if param.ParameterType.IsFSharp then
            match param.ParameterType.FullName with
            | "Microsoft.FSharp.Core.FSharpRef`1" ->
                param.ParameterType <- param.ParameterType.GenericParameters.[0]
                param.IsIn <- true
            
            | _  ->
                match getTypeReplacement(param.ParameterType) with
                | null -> logWarning "Unable to find replacement type for %O." param.ParameterType
                | repl -> param.ParameterType <- repl

    member __.ReplaceVariable(var: VariableDefinition) =
        if var.VariableType.IsFSharp then
            match getTypeReplacement(var.VariableType) with
            | null -> logWarning "Unable to find replacement type for %O." var.VariableType
            | repl -> var.VariableType <- repl

    member __.PurgeMethod(method: MethodDefinition) =
        // 1. Clean attributes
        for i, attr in method.CustomAttributes.GetMutableEnumerator() do
            if attr.AttributeType.IsFSharp then
                method.CustomAttributes.RemoveAt(i)

        // 2. Replace parameter and return types
        if method.ReturnType.IsFSharp then
            match getTypeReplacement(method.ReturnType) with
            | null -> logWarning "Unable to find replacement type for %O." method.ReturnType
            | typ -> method.ReturnType <- typ

        for param in method.Parameters do
            this.ReplaceParameter(param)

        // 3. Replace variables
        for var in method.Body.Variables do
            this.ReplaceVariable(var)

        // 4. Replace member references and function calls
        this.PurgeMethodBody(method)
        
        false
    
    member __.PurgeMethodBody(method: MethodDefinition) =
        // Set up local variables
        let body = method.Body
        let instrs = body.Instructions

        let inline instrsAtWithLen(start, len) =
            [| for i = 0 to len do yield instrs.[start + i] |]
        let inline lazyInstrsAtWithLen(start, len) =
            lazy instrsAtWithLen(start, len)

        // Recursively process calls
        let mutable i = 0

        // This stack keeps track of every element currently on the stack by saving their
        // (valueStart, valueLength).
        let stack = Stack()

        while i < instrs.Count do
            let instr = instrs.[i]
            
            let mutable groupStart = 0
            let mutable args = [||]

            match instr.StackChanges with
            | 0, 0 -> ()
            | 0, 1 -> stack.Push(i, 1)
            
            | 1, 1 -> let start, len = stack.Pop()
                      stack.Push(start, len + 1)

                      groupStart <- start
                      args <- [| lazyInstrsAtWithLen(start, len) |]
            
            | 1, 2 -> let start, len = stack.Pop()
                      stack.Push(start, len + 1)
                      stack.Push(i, 1)

                      groupStart <- start
                      args <- [| lazyInstrsAtWithLen(start, len) ; lazyInstrsAtWithLen(i, 1) |]

            | n, 1 -> let mutable start, len = stack.Pop()

                      groupStart <- start

                      args <- Array.zeroCreate n
                      args.[0] <- lazyInstrsAtWithLen(start, len)

                      for i in 1..n do
                        let s, l = stack.Pop()

                        args.[i] <- lazyInstrsAtWithLen(s, l)
                        start    <- min start s
                        len      <- len + l
                      
                      stack.Push(start, len)

            | n, p -> logWarning "Unsupported stack operation (removed %d and added %d) in %O." n p method

            match instr.Operand with
            | :? TypeReference as typ when typ.IsFSharp ->
                match instr.OpCode.Code with
                | Code.Ldtoken ->
                    // typeof<'T> -> null
                    instr.OpCode  <- OpCodes.Ldnull
                    instr.Operand <- null

                    logWarning "Removing typeof<%s> in %s." typ.FullName method.FullName
                
                | Code.Newarr | Code.Newobj ->
                    // new FSharpType[] -> new ReplacementType[]
                    // new FSharpType() -> new ReplacementType()
                    instr.Operand <- getTypeReplacement typ
            
                | _ ->
                    logWarning "Unhandled type reference in %s (opcode: %O)." method.FullName instr.OpCode
                
                i <- i + 1

            | :? FieldReference as fld when fld.DeclaringType.IsFSharp ->
                // FSharpType.fieldAccess
                let start, len, arg =
                    match instr.OpCode.Code with
                    | Code.Ldfld  -> groupStart, i - groupStart, Some(args.[0])
                    | Code.Ldsfld -> i, 0, None

                    | _ ->
                        logWarning "Unhandled field reference in %s (opcode: %O)." method.FullName instr.OpCode
                        0, 0, None

                let toReplace   = instrsAtWithLen(start, len)
                let replacement = replaceFieldAccess(fld, toReplace, arg, this.LogDebug, this.LogWarning, this.LogError)
                let prevLen     = instrs.Count
                
                instrs.ReplaceRange(start, start + len, replacement)
                
                i <- i + 1 + instrs.Count - prevLen
            
            | :? MethodReference as method when method.DeclaringType.IsFSharp ->
                // Call(a, b)
                let toReplace   = instrsAtWithLen(groupStart, i - groupStart)
                let replacement = replaceCall(method, toReplace, args, this.LogDebug, this.LogWarning, this.LogError)
                let prevLen     = instrs.Count

                instrs.ReplaceRange(groupStart, i, replacement)

                i <- i + 1 + instrs.Count - prevLen

            | _ -> i <- i + 1
