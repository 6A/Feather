module Extensions

open System.Collections.Generic

type IEnumerable<'T> with
    member this.Length = Seq.length this
    // member this.Append(x: 'T) = seq { yield x ; yield! this }
