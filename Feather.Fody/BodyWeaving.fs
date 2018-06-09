module internal BodyWeaving

open System

open Mono.Cecil
open Mono.Cecil.Cil

let replaceCall(target: MethodReference, instrs: Instruction[], args: Lazy<Instruction[]>[],
                logDebug: Action<string>, logWarning: Action<string>, logError: Action<string>) =
    
    let inline logDebug str   = Printf.ksprintf logDebug.Invoke str
    let inline logWarning str = Printf.ksprintf logWarning.Invoke str
    let inline logError str   = Printf.ksprintf logError.Invoke str

    instrs

let replaceFieldAccess(target: FieldReference, instrs: Instruction[], arg: Lazy<Instruction[]> option,
                       logDebug: Action<string>, logWarning: Action<string>, logError: Action<string>) =

    let inline logDebug str   = Printf.ksprintf logDebug.Invoke str
    let inline logWarning str = Printf.ksprintf logWarning.Invoke str
    let inline logError str   = Printf.ksprintf logError.Invoke str
    
    instrs
