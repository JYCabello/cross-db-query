open System
open CrossDdQueryFs

[<EntryPoint>]
let main _ =
    MigrationMultiProfile.program() |> Async.RunSynchronously
    MigrationDashboard.program() |> Async.RunSynchronously
    printf "Press a key to end"
    Console.ReadKey() |> ignore
    0
