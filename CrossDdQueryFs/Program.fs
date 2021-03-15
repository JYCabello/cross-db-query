open System
open CrossDdQueryFs

[<EntryPoint>]
let main _  =
    //DelinquentUsers.program() |> Async.RunSynchronously
    let usersProfiles = DataLayer.Repository.usersProfilesTotal () |> Async.RunSynchronously
    printf "%A" usersProfiles
    let _ = DataLayer.Repository.setUsersProfiles (usersProfiles) |> Async.RunSynchronously
    printf "Press a key to end"
    Console.ReadKey() |> ignore
    0
