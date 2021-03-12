open System
open CrossDdQueryFs

[<EntryPoint>]
let main _  =
    DelinquentUsers.program() |> Async.RunSynchronously
    printf "Press a key to end"
    Console.ReadKey() |> ignore
    0
