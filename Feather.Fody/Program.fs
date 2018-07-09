module Program

open System
open System.IO
open System.Xml.Linq

open Feather
open Fody

open Mono.Cecil

let inline logWithColor prefix color = Action<string>(fun str ->
    Console.ForegroundColor <- color
    Console.Write("{0} ", (prefix : string))
    Console.ResetColor()
    Console.WriteLine str
)

/// Entry point used when debugging the modification process.
[<EntryPoint>]
let main _ =
    let assemblyPath = Path.Combine(__SOURCE_DIRECTORY__, "..", "Feather.TestAssembly",
                                    "bin", "Debug", "netcoreapp2.0",
                                    "Feather.TestAssembly.dll")
    
    let assembly = AssemblyDefinition.ReadAssembly(assemblyPath)
    let weaver   = ModuleWeaver()

    weaver.AssemblyFilePath      <- assemblyPath
    weaver.Config                <- XElement(XName.Get("Feather"))
    weaver.ModuleDefinition      <- assembly.MainModule
    weaver.SolutionDirectoryPath <- Path.GetDirectoryName(assemblyPath)

    weaver.LogMessage <- Action<_, _>(fun msg -> function 
        | MessageImportance.Normal -> weaver.LogWarning.Invoke msg
        | MessageImportance.High   -> weaver.LogError.Invoke msg
        | _                        -> weaver.LogDebug.Invoke msg
    )

    weaver.LogDebug   <- logWithColor "[i]" ConsoleColor.Blue
    weaver.LogError   <- logWithColor "[!]" ConsoleColor.Red
    weaver.LogWarning <- logWithColor "[-]" ConsoleColor.Yellow

    weaver.Execute()
    weaver.AfterWeaving()

    0
