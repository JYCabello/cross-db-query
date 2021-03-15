open System
open CrossDdQueryFs

[<EntryPoint>]
let main _  =
    DelinquentUsers.program() |> Async.RunSynchronously
    let mah = DataLayer.Repository.usersProfilesTotal () |> Async.RunSynchronously
    printf "%A" mah
    let meh = DataLayer.Repository.setUsersProfiles (mah) |> Async.RunSynchronously
    printf "%A" meh
    printf "Press a key to end"
    Console.ReadKey() |> ignore
    0
