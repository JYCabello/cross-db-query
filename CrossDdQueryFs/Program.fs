open System
open CrossDdQueryFs

[<EntryPoint>]
let main _  =
    Migration.program() |> Async.RunSynchronously
    printf "Press a key to end"
    Console.ReadKey() |> ignore
    0
