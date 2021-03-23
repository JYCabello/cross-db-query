open System
open CrossDdQueryFs
open DataLayer

[<EntryPoint>]
let main _  =
    Repository.setDashboards () |> Async.RunSynchronously
    printf "Press a key to end"
    Console.ReadKey() |> ignore
    0
