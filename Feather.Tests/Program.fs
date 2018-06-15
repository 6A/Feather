module Program

open System.Collections.Generic
open System.Reflection

open NUnit.Framework.Api
open NUnit.Framework.Internal

/// Entry point of the program, used when debugging tests through the
/// .NET Core debugger.
[<EntryPoint>]
let main _ =
    let builder = DefaultTestAssemblyBuilder()
    let runner = NUnitTestAssemblyRunner(builder)

    // Load tests that ensure the resulting assembly is still valid.
    runner.Load(typeof<Feather.TestAssembly.TestClass>.Assembly, Dictionary()) |> ignore
    
    // Load tests that ensure all F#-related data has been removed.
    runner.Load(Assembly.GetExecutingAssembly(), Dictionary()) |> ignore

    // Return the number of inconclusive tests.
    // If no test was inconclusive, STATUS_OK (0) is returned.
    runner.Run(TestListener.NULL, TestFilter.Empty).InconclusiveCount
