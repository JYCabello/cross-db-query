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
      ProfileId = u |> getProfileId usersProfiles
      Roles = (u |> getRoles userRoleRows)
      Email = u.Email
    })

let roleIdInList roleIds role =
  not (roleIds |> List.exists (fun rId -> role.Id = rId))


let toDelinquent userProfileRows roleProfileRows (allProfiles: Profile list) (allRoles: Role list) (user: UserWithRoles) =
  let profileRoleIds = user |> getProfileRoleIds userProfileRows roleProfileRows
  let extraRoles = user.Roles |> List.where (fun r -> not (profileRoleIds |> List.exists (fun pr -> r.Id = pr)))
  let defaultUnknown opt = opt |> Option.defaultValue "unknown"
  let missingRoles =
   profileRoleIds
     |> List.filter (fun id -> not (user.Roles |> List.exists (fun r -> r.Id = id)))
     |> List.map (fun id -> allRoles |> List.find (fun r -> r.Id = id))
  {
    Id= user.Id
    ProfileId=
      user.ProfileId
        |> Option.map (sprintf "%A")
        |> defaultUnknown
    ProfileName=
      option {
        let! pid = user.ProfileId
        let! profile = allProfiles |> List.tryFind (fun p -> p.Id = pid)
        return profile.Name
      } |> defaultUnknown
    Email= user.Email |> defaultUnknown
    ExtraRoles= extraRoles |> List.map (fun r -> r.Name)
    MissingRoles= missingRoles |> List.map (fun r -> r.Name)
  }

let hasIncorrectRoles userProfileRows rolesPerProfile user =
  let profileRoleIds = user |> getProfileRoleIds userProfileRows rolesPerProfile
  let hasExtraRoles = user.Roles |> List.exists (fun r -> not (profileRoleIds |> List.exists (fun pr -> r.Id = pr)))
  let hasMissingRoles = profileRoleIds |> List.exists (fun prId -> not (user.Roles |> List.exists (fun r -> prId = r.Id)))
  hasExtraRoles || hasMissingRoles

let program() =
  let userRoleRows = Repository.getUserRoleRows()
  let rolesPerProfile = Repository.getRolesPerProfile()
  let userProfileRows = Repository.getUsersProfileRows()
  let allProfiles = Repository.getProfiles()
  let allRoles = Repository.getAllRoles()
  let distinctUsers = userRoleRows |> toDistinctUsers userProfileRows 
  let delinquentUsers =
    distinctUsers 
      |> List.filter (hasIncorrectRoles userProfileRows rolesPerProfile)
      |> List.map (toDelinquent userProfileRows rolesPerProfile allProfiles allRoles)
  printfn "%A" delinquentUsers
  printfn "There was a total of %i users with non matching roles over a totale of %i users"
    (delinquentUsers |> List.length)
    (distinctUsers |> List.length)
  File.WriteAllLines("output.txt", delinquentUsers |> List.map (sprintf "%A"))

[<EntryPoint>]
let main _  =
    program()
    printf "Press a key to end"
    Console.ReadKey() |> ignore
    0
