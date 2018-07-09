module internal BodyWeaving

open System
open System.Linq

open Fody
open Mono.Cecil
open Mono.Cecil.Cil


let replaceCall(target: MethodReference, instrs: Instruction[], args: Lazy<Instruction[]>[],
                w: BaseModuleWeaver) =
    
    let inline logDebug str   = Printf.ksprintf w.LogDebug.Invoke str
    let inline logWarning str = Printf.ksprintf w.LogWarning.Invoke str
    let inline logError str   = Printf.ksprintf w.LogError.Invoke str

    match (sprintf "%O::%s" target.DeclaringType target.Name) with
    | "Microsoft.FSharp.Collections.SeqModule::Length" ->
        match w.ModuleDefinition.MethodOf(Enumerable.Count Seq.empty) with
        | :? GenericInstanceMethod as method ->
            method.GenericArguments.[0] <- (target :?> GenericInstanceMethod).GenericArguments.[0]
            instrs.[instrs.Length - 1].Operand <- method

        | _ -> logWarning "Unable to resolve System.Linq.Enumerable."
    
    | unknown ->
        logWarning "Unable to replace call to %s." unknown

    instrs

let replaceFieldAccess(target: FieldReference, instrs: Instruction[], w: BaseModuleWeaver) =

    let inline logDebug str   = Printf.ksprintf w.LogDebug.Invoke str
    let inline logWarning str = Printf.ksprintf w.LogWarning.Invoke str
    let inline logError str   = Printf.ksprintf w.LogError.Invoke str

    logWarning "Unable to replace field access to %O." target
    
    instrs
