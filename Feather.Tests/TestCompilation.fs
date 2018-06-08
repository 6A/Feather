module TestCompilation

open System.IO

open Swensen.Unquote
open NUnit.Framework

open Mono.Cecil
open Mono.Cecil.Cil

let private assemblyPath =
    let sub = if inDebugMode then "Debug" else "Release"

    Path.Combine(__SOURCE_DIRECTORY__, "..", "Feather.TestAssembly", "bin",
                   sub, "netcoreapp2.0", "Feather.TestAssembly.dll")

let private assembly = AssemblyDefinition.ReadAssembly(assemblyPath)
let private modl = assembly.MainModule


let references = modl.AssemblyReferences

[<Test; TestCaseSource("references")>]
let ``should have no reference to FSharp.Core``(reference: AssemblyNameReference) =
    reference.Name <>! "FSharp.Core"


let assemblyAttributes = assembly.CustomAttributes
let moduleAttributes = modl.CustomAttributes

[<Test; TestCaseSource("assemblyAttributes")>]
let ``should remove all FSharp attributes on assembly``(attr: CustomAttribute) =
    attr.AttributeType.IsFSharp =! false

[<Test; TestCaseSource("moduleAttributes")>]
let ``should remove all FSharp attributes on module``(attr: CustomAttribute) =
    attr.AttributeType.IsFSharp =! false


let allAttributes = seq<IMemberDefinition> {
    let inline convert collection = seq { for item in collection -> item :> IMemberDefinition }
    
    for typ in modl.GetTypes() do
        yield typ :> _

        yield! convert typ.Fields
        yield! convert typ.Properties
        yield! convert typ.Events
        yield! convert typ.Methods
        yield! convert typ.Fields
}

[<Test; TestCaseSource("allAttributes")>]
let ``should remove all FSharp attributes on types and members``(m: IMemberDefinition) =
    m.CustomAttributes.HasFSharpAttribute() =! false


let allMethods      = seq { for typ in modl.GetTypes() do yield! typ.Methods }
let allParameters   = seq { for method in allMethods   do yield! method.Parameters }
let allVariables    = seq { for method in allMethods   do yield! method.Body.Variables }
let allInstructions = seq { for method in allMethods   do yield! method.Body.Instructions }

[<Test; TestCaseSource("allParameters")>]
let ``should remove all FSharp parameters``(parameter : ParameterDefinition) =
    parameter.ParameterType.IsFSharp =! false

[<Test; TestCaseSource("allVariables")>]
let ``should remove all FSharp variables``(variable : VariableDefinition) =
    variable.VariableType.IsFSharp =! false

[<Test; TestCaseSource("allInstructions")>]
let ``should remove all FSharp calls``(instr : Instruction) =
    match instr.Operand with
    | :? MemberReference as m -> m.IsFSharp =! false
    | _ -> ()
