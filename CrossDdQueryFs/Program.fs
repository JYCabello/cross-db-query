open System

[<EntryPoint>]
let main _  =
    printf "Press a key to end"
    Console.ReadKey() |> ignore
    0
