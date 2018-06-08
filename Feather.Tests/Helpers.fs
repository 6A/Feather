[<AutoOpen>]
module Helpers

open System.Runtime.CompilerServices

open Mono.Cecil


let inDebugMode =
#if DEBUG
    true
#else
    false
#endif

type MemberReference with
    member this.IsFSharp =
        match this with
        | :? TypeReference as ty -> ty.Namespace.Contains "FSharp"
        | other -> other.DeclaringType.Namespace.Contains "FSharp"


[<Extension>]
type CollectionExtensions() =
    [<Extension>]
    static member inline HasFSharpItem<'S, 'T when 'T :> MemberReference and 'S :> 'T seq>(xs: 'S) =
        Seq.exists (fun (x: 'T) -> x.IsFSharp) xs
    
    [<Extension>]
    static member inline HasFSharpAttribute<'S when 'S :> CustomAttribute seq>(xs: 'S) =
        Seq.exists (fun (x: CustomAttribute) -> x.AttributeType.IsFSharp) xs