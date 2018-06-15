[<AutoOpen>]
module internal Helpers

open System.IO
open System.Linq

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

open Mono.Cecil
open Mono.Cecil.Cil
open Mono.Collections.Generic

type IMetadataScope with
    /// Returns whether this scope corresponds to FSharp.Core.
    member this.IsFSharp = this.Name = "FSharp.Core"

type TypeReference with
    /// Returns whether the declaring module of this type is FSharp.Core.
    member this.IsFSharp =
        this.Scope.IsFSharp || (not (isNull this.DeclaringType) && this.DeclaringType.IsFSharp)
    
    /// Returns whether this type is `void`.
    member this.IsVoid =
        this.FullName = "System.Void"

type TypeDefinition with
    /// Returns the first method bearing the given name.
    member this.GetMethod(s) = this.Methods.FirstOrDefault(fun x -> x.Name = s)

    /// Returns the field bearing the given name.
    member this.GetField(s) = this.Fields.FirstOrDefault(fun x -> x.Name = s)

type MemberReference with
    /// Returns whether the declaring module of this member is FSharp.Core.
    member this.IsFSharp = this.DeclaringType.Scope.IsFSharp

type Collection<'T> with
    /// Returns an enumerator that allows suppression of the current item
    /// while enumeration.
    member this.GetMutableEnumerator() = seq {
        let mutable i = 0

        while i < this.Count do
            let j = this.Count

            yield i, this.[i]

            i <- i + 1 + (this.Count - j)
    }

    /// Replaces the specified (inclusive) range by the given items.
    member this.ReplaceRange(from: int, upto: int, items: 'T seq) =
        for i = 0 to from - upto do
            this.RemoveAt(i)
        
        for i, item in Seq.indexed items do
            this.Insert(from + i, item)


type Collection<'T> with
    /// Removes all enements that match the given predicate in the collection.
    member this.Remove(condition) =
        for i, item in this.GetMutableEnumerator() do
            if condition item then
                this.RemoveAt(i)

type ModuleDefinition with
    member inline this.MethodOf([<ReflectedDefinition>] m: Expr<_>) =
        match m with Call(_, m, _) -> this.ImportReference m
                   | _             -> null

    member inline this.FieldOf([<ReflectedDefinition>] f: Expr<_>) =
        match f with FieldSet(_, f, _) | FieldGet(_, f) -> this.ImportReference f
                   | _                                  -> null
    
    member inline this.TypeOf<'T>() =
        this.ImportReference typeof<'T>


type StackBehaviour with
    /// Returns the number of items added to the stack by this operation.
    member this.StackChange =
        match this with
        | StackBehaviour.Push1_push1 -> 2

        | StackBehaviour.Push1
        | StackBehaviour.Pushi
        | StackBehaviour.Pushi8
        | StackBehaviour.Pushr4
        | StackBehaviour.Pushr8
        | StackBehaviour.Pushref -> 1

        | StackBehaviour.Pop0
        | StackBehaviour.Push0 -> 0
        
        | StackBehaviour.Popi_pop1
        | StackBehaviour.Popi_popi
        | StackBehaviour.Popi_popi8
        | StackBehaviour.Popi_popr4
        | StackBehaviour.Popi_popr8
        | StackBehaviour.Popref_pop1
        | StackBehaviour.Pop1_pop1
        | StackBehaviour.Popref_popi -> -2
        
        | StackBehaviour.Popi_popi_popi
        | StackBehaviour.Popref_popi_popi
        | StackBehaviour.Popref_popi_popi8
        | StackBehaviour.Popref_popi_popr4
        | StackBehaviour.Popref_popi_popr8
        | StackBehaviour.Popref_popi_popref -> -3
        
        | _ -> -1

let inline private returnSize(method : MethodReference) =
    if method.ReturnType.IsVoid then 0 else 1

let inline private instanceSize(method : MethodReference) =
    if method.HasThis then 1 else 0

type Instruction with
    /// Returns the number of items added to the stack by this operation.
    member this.GetStackChange(returnsVoid: bool) =
        match this.OpCode.Code with
        | Code.Call | Code.Calli | Code.Callvirt ->
            match this.Operand with
            | :? MethodReference as method ->
                returnSize method - method.Parameters.Count - instanceSize method
            | _ ->
                raise <| InvalidDataException()
        
        | Code.Ret -> if returnsVoid then 0 else -1

        | _ -> this.OpCode.StackBehaviourPop.StackChange + this.OpCode.StackBehaviourPush.StackChange

    /// Returns a (number of popped items, number of pushed items) pair.
    member this.GetStackChanges(returnsVoid: bool) =
        match this.OpCode.Code with
        | Code.Call | Code.Calli | Code.Callvirt ->
            match this.Operand with
            | :? MethodReference as method ->
                method.Parameters.Count + instanceSize method, returnSize method
            | _ ->
                raise <| InvalidDataException()
        
        | Code.Ret -> (if returnsVoid then 0 else 1), 0

        | _ -> -this.OpCode.StackBehaviourPop.StackChange, this.OpCode.StackBehaviourPush.StackChange
