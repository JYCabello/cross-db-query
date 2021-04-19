module CrossDdQueryFs.DelinquentUsers

open System
open System.IO
open DataLayer
open DataLayer.DataModels
open Utils

type UserWithRoles =
  { Id: Guid
    ProfileId: Guid option
    Email: string option
    Roles: Role list }

type DelinquentUser = 
  { Id: Guid
    ProfileId: string
    ProfileName: string
    Email: string
    ExtraRoles: string list
    MissingRoles: string list }

let getProfileRoleIds (usersProfiles: UserProfileRow list) (rolesPerProfile: RoleProfileRow list) (user: UserWithRoles) =
  OptionUtils.option {
    let! userProfile = usersProfiles |> List.tryFind (fun up -> up.UserId = user.Id) 
    let! profileID = userProfile.PossibleProfileId
    return rolesPerProfile |> List.filter (fun rpp -> rpp.ProfileId = profileID) |> List.map (fun rpp -> rpp.RoleId)
  } |> Option.defaultValue []

let getProfileId (usersProfiles: UserProfileRow list) (u: UserRoleRow) =
  usersProfiles 
  |> List.tryFind (fun up -> up.UserId = u.UserId)
  |> Option.bind (fun up -> up.PossibleProfileId)

let getRoles (userRoleRows: UserRoleRow list) (user: UserRoleRow) : Role list =
  userRoleRows
  |> List.filter (fun u -> u.UserId = user.UserId)
  |> List.map (fun u -> {Id= u.RoleId; Name= u.RoleName})

let toDistinctUsers (usersProfiles: UserProfileRow list) (userRoleRows: UserRoleRow list) =
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
      |> Option.bind (fun pid -> allProfiles |> List.tryFind (fun p -> p.ID = pid))
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
    let! userRoles, rolesPerProfile, userProfiles, allProfiles, allRoles =
      Parallel.tuple (R.usersRoles(), R.rolesPerProfile(), R.usersProfilesBySettings(), R.profiles(), R.roles())
    let distinctUsers = toDistinctUsers userProfiles userRoles
    let delinquentUsers =
      distinctUsers 
      |> List.filter (hasIncorrectRoles userProfiles rolesPerProfile)
      |> List.map (toDelinquent userProfiles rolesPerProfile allProfiles allRoles)
    printfn $"%A{delinquentUsers}"
    printfn $"Users with non matching roles: %i{delinquentUsers |> List.length} \nTotal: %i{distinctUsers |> List.length}"
    File.WriteAllLines("output.txt", List.map (sprintf "%A") delinquentUsers)
  }
