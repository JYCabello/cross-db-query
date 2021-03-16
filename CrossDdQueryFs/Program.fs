open System

[<EntryPoint>]
let main _  =
    let usersProfiles = DataLayer.Repository.usersProfilesTotal () |> Async.RunSynchronously
    printf "%A" usersProfiles
    let _ = DataLayer.Repository.setUsersProfiles (usersProfiles) |> Async.RunSynchronously
    printf "Press a key to end"
    Console.ReadKey() |> ignore
    0
