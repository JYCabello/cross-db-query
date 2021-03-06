open System
open System.IO
open DataLayer
open DataModels
open Utils

type UserWithRoles =
  { Id: Guid
    ProfileId: Guid option
    Email: string option
    Roles: Role list }
//Add missing roles
type DelinquentUser = 
  { Id: Guid
    ProfileId: string
    ProfileName: string
    Email: string
    ExtraRoles: string list
    MissingRoles: string list }

let getProfileRoleIds (usersProfiles: UserProfile list) (rolesPerProfile: RoleProfileRow list) (user: UserWithRoles) =
  usersProfiles
    |> List.tryFind (fun up -> up.UserId = user.Id) 
    |> Option.bind (fun up -> up.ProfileId)
    |> Option.map 
      (fun pId -> 
        rolesPerProfile 
          |> List.filter (fun rpp -> rpp.ProfileId = pId) 
          |> List.map (fun rpp -> rpp.RoleId))
    |> Option.defaultValue []

let getProfileId usersProfiles (u: UserRoleRow) =
  usersProfiles 
    |> List.tryFind (fun up -> up.UserId = u.UserId) 
    |> Option.bind (fun up -> up.ProfileId)

let getRoles (userRoleRows: UserRoleRow list) (user: UserRoleRow) : Role list =
  userRoleRows
    |> List.filter (fun u -> u.UserId = user.UserId)
    |> List.map (fun u -> {Id= u.RoleId; Name= u.RoleName})

let toDistinctUsers usersProfiles (userRoleRows: UserRoleRow list) =
  userRoleRows
    |> List.distinctBy (fun u -> u.UserId)
    |> List.map (fun u -> {
      Id = u.UserId
      ProfileId = getProfileId usersProfiles u
      Roles = getRoles userRoleRows u
      Email = u.Email
    })

let toDelinquent userProfileRows roleProfileRows (allProfiles: Profile list) (allRoles: Role list) (user: UserWithRoles) =
  let profileRoleIds = getProfileRoleIds userProfileRows roleProfileRows user
  let extraRoles = user.Roles |> List.where (fun r -> not (profileRoleIds |> List.exists (fun pr -> r.Id = pr)))
  let defaultUnknown opt = Option.defaultValue "unknown" opt
  let missingRoles =
   profileRoleIds
     |> List.filter (fun id -> not (user.Roles |> List.exists (fun r -> r.Id = id)))
     |> List.map (fun id -> allRoles |> List.find (fun r -> r.Id = id))
  {
    Id = user.Id
    ProfileId =
      user.ProfileId
        |> Option.map (sprintf "%A")
        |> defaultUnknown
    ProfileName =
      user.ProfileId
        |> Option.bind (fun pid -> allProfiles |> List.tryFind (fun p -> p.Id = pid))
        |> Option.map (fun p -> p.Name)
        |> defaultUnknown
    Email = user.Email |> defaultUnknown
    ExtraRoles = extraRoles |> List.map (fun r -> r.Name)
    MissingRoles = missingRoles |> List.map (fun r -> r.Name)
  }

let hasIncorrectRoles userProfileRows rolesPerProfile user =
  let profileRoleIds = getProfileRoleIds userProfileRows rolesPerProfile user
  let hasExtraRoles = user.Roles |> List.exists (fun r -> not (profileRoleIds |> List.exists (fun pr -> r.Id = pr)))
  let hasMissingRoles = profileRoleIds |> List.exists (fun prId -> not (user.Roles |> List.exists (fun r -> prId = r.Id)))
  hasExtraRoles || hasMissingRoles

module R = Repository
let program () =
  async {
    let! (userRoleRows, rolesPerProfile, userProfileRows, allProfiles, allRoles) =
      parallelTuple5(R.usersRoles(), R.rolesPerProfile(), R.usersProfiles(), R.profiles(), R.roles())
    let distinctUsers = toDistinctUsers userProfileRows userRoleRows
    let delinquentUsers =
      distinctUsers 
        |> List.filter (hasIncorrectRoles userProfileRows rolesPerProfile)
        |> List.map (toDelinquent userProfileRows rolesPerProfile allProfiles allRoles)
    printfn "%A" delinquentUsers
    printfn $"Users with non matching roles: {delinquentUsers |> List.length} \nTotal: {distinctUsers |> List.length}"
    File.WriteAllLines("output.txt", List.map (sprintf "%A") delinquentUsers)
  }
  

[<EntryPoint>]
let main _  =
    program() |> Async.RunSynchronously
    printf "Press a key to end"
    Console.ReadKey() |> ignore
    0
