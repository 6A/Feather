module internal BodyWeaving

open System
open System.Collections.Generic
open System.Linq

open Mono.Cecil
open Mono.Cecil.Cil

open Replacements

let weave( logDebug: Action<string>
         , logWarning: Action<string>
         , logError: Action<string>
         , method: MethodDefinition   ) =

    // Set up local variables
    let body = method.Body
    let instrs = body.Instructions

    let inline logDebug str   = Printf.ksprintf logDebug.Invoke str
    let inline logWarning str = Printf.ksprintf logWarning.Invoke str
    let inline logError str   = Printf.ksprintf logError.Invoke str

    let replaceCall(target: MethodReference, instr: Instruction[], args: (int * int)[]) =
        instr :> _ seq

    let replaceFieldAccess(target: FieldReference, instr: Instruction[], arg: (int * int) option) =
        instr :> _ seq

    // Recursively process calls
    let mutable i = 0

    // This stack keeps track of every element currently on the stack by saving their
    // (valueStart, valueLength).
    let stack = Stack()

    // The goal is to find
    while i < instrs.Count do
        let instr = instrs.[i]
        let mutable args = [||]

        match instr.StackChanges with
        | 0, 0 -> ()
        | 0, 1 -> stack.Push(i, 1)
        
        | 1, 1 -> let start, len = stack.Pop()
                  stack.Push(start, len + 1)

                  args <- [| start, len |]
        
        | 1, 2 -> let start, len = stack.Pop()
                  stack.Push(start, len + 1)
                  stack.Push(i, 1)

                  args <- [| start, len ; i, 1 |]

        | n, 1 -> let mutable start, len = stack.Pop()

                  args <- Array.zeroCreate n
                  args.[0] <- start, len

                  for i in 1..n do
                    let s, l = stack.Pop()

                    args.[i] <- s, l
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
                | Code.Ldfld  -> let start, len = args.[0]
                                 start, len + 1, Some(start, len)
                
                | Code.Ldsfld -> i, 0, None
                
                | _ ->
                    logWarning "Unhandled field reference in %s (opcode: %O)." method.FullName instr.OpCode
                    0, 0, None

            let toReplace = [| for i = 0 to len do yield instrs.[start + i] |]
            let replacement = replaceFieldAccess(fld, toReplace, arg)
            
            instrs.ReplaceRange(start, start + len, replacement)
            
            i <- i + 1
        
        | :? MethodReference as method when method.DeclaringType.IsFSharp ->
            // Call(a, b)
            let start, len = match args.Length with
                             | 0 -> i, 0
                             | _ -> args.[0]
            
            let toReplace   = [| for i = 0 to len do yield instrs.[start + i] |]
            let replacement = replaceCall(method, toReplace, args)
            let prevLen     = instrs.Count

            instrs.ReplaceRange(start, start + len, replacement)

            i <- i + 1 + instrs.Count - prevLen

        | _ -> i <- i + 1

    ()
