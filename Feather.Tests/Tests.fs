module TestCompilation

open System.IO

open Swensen.Unquote
open NUnit.Framework

open Mono.Cecil
open Mono.Cecil.Cil

let private assemblyPath =
    let sub = if inDebugMode then "Debug" else "Release"

    Path.Combine(__SOURCE_DIRECTORY__, "..", "Feather.TestAssembly", "bin",
                   sub, "netstandard2.0", "Feather.TestAssembly.dll")

let private assembly = AssemblyDefinition.ReadAssembly(assemblyPath)
let private modl = assembly.MainModule


let references = modl.AssemblyReferences

[<Test; TestCaseSource("references")>]
let ``should have no reference to FSharp.Core``(reference : AssemblyNameReference) =
    reference.Name <>! "FSharp.Core"


let assemblyAttributes = assembly.CustomAttributes
let moduleAttributes = modl.CustomAttributes

[<Test; TestCaseSource("assemblyAttributes")>]
let ``should remove all FSharp attributes on assembly``(attr : CustomAttribute) =
    attr.AttributeType.IsFSharp =! false

[<Test; TestCaseSource("moduleAttributes")>]
let ``should remove all FSharp attributes on module``(attr : CustomAttribute) =
    attr.AttributeType.IsFSharp =! false


let allAttributes = seq {
    let inline convert collection = seq {
        for item in collection -> TestCaseData(item :> obj).SetName(sprintf "%O should have no F# attribute." item)
    }
    
    for typ in modl.GetTypes() do
        yield TestCaseData(typ).SetName(sprintf "%O should have no F# attribute." typ)

        yield! convert typ.Fields
        yield! convert typ.Properties
        yield! convert typ.Events
        yield! convert typ.Methods
}

[<Test; TestCaseSource("allAttributes")>]
let ``should remove all FSharp attributes on types and members``(m : IMemberDefinition) =
    m.CustomAttributes.HasFSharpAttribute() =! false

let allMethods = seq {
    for typ in modl.GetTypes() do
        for method in typ.Methods do
            yield TestCaseData(method).SetName(sprintf "%O should not return a F# type." method)
}

let allFields = seq {
    for typ in modl.GetTypes() do
        for field in typ.Fields do
            yield TestCaseData(field).SetName(sprintf "%O should not contain a F# type." field)
}

let allProperties = seq {
    for typ in modl.GetTypes() do
        for prop in typ.Properties do
            yield TestCaseData(prop).SetName(sprintf "%O should not return a F# type." prop)
}

let allEvents = seq {
    for typ in modl.GetTypes() do
        for event in typ.Events do
            yield TestCaseData(event).SetName(sprintf "%O should not return a F# type." event)
}

[<Test; TestCaseSource("allMethods")>]
let ``should remove all FSharp-returning methods``(m : MethodDefinition) =
    m.ReturnType.IsFSharp =! false

[<Test; TestCaseSource("allFields")>]
let ``should remove all FSharp-returning fields``(f : FieldDefinition) =
    f.FieldType.IsFSharp =! false

[<Test; TestCaseSource("allProperties")>]
let ``should remove all FSharp-returning properties``(f : PropertyDefinition) =
    f.PropertyType.IsFSharp =! false

[<Test; TestCaseSource("allEvents")>]
let ``should remove all FSharp-returning events``(f : EventDefinition) =
    f.EventType.IsFSharp =! false


let inline private generateTestData getSrc getName = seq {
    for typ in modl.GetTypes() do
        for method in typ.Methods do
            for item in getSrc method do
                yield TestCaseData(method, item).SetName(getName method item)
}

let allParameters =
    generateTestData
        (fun m -> m.Parameters)
        (fun m param -> sprintf "Parameter %s of method %O.%s should have been replaced." param.Name m.DeclaringType m.Name)

let allVariables =
    generateTestData
        (fun m -> m.Body.Variables)
        (fun m var -> sprintf "Variable #%d of method %O.%s should have been replaced." var.Index m.DeclaringType m.Name)

let allInstructions =
    generateTestData
        (fun m -> m.Body.Instructions)
        (fun m instr -> sprintf "Instruction [%O] of method %O.%s should have been replaced." instr m.DeclaringType m.Name)

[<Test; TestCaseSource("allParameters")>]
let ``should remove all FSharp parameters``(_: MethodDefinition, parameter : ParameterDefinition) =
    parameter.ParameterType.IsFSharp =! false

[<Test; TestCaseSource("allVariables")>]
let ``should remove all FSharp variables``(_: MethodDefinition, variable : VariableDefinition) =
    variable.VariableType.IsFSharp =! false

[<Test; TestCaseSource("allInstructions")>]
let ``should remove all FSharp calls``(_: MethodDefinition, instr : Instruction) =
    match instr.Operand with
    | :? MemberReference as m -> m.IsFSharp =! false
    | _ -> ()
