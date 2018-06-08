[<AutoOpen>]
module Helpers

open Mono.Cecil
open Mono.Collections.Generic

type IMetadataScope with
    /// Returns whether this scope corresponds to FSharp.Core.
    member this.IsFSharp = this.Name = "FSharp.Core"

type TypeReference with
    /// Returns whether the declaring module of this member is FSharp.Core.
    member this.IsFSharp = this.Scope.IsFSharp

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

type Collection<'T> with
    /// Removes all enements that match the given predicate in the collection.
    member this.Remove(condition) =
        for i, item in this.GetMutableEnumerator() do
            if condition item then
                this.RemoveAt(i)
