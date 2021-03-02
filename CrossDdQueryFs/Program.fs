﻿open System
open System.IO
open CrossDdQueryFs
open DataLayer
open DataModels
open Utils

type UserWithRoles = {id:Guid; profileId:Guid option; email:string option; roles:Role list}
//Add missing roles
type DelinquentUser = 
  {
    id:Guid
    profileId:string
    profileName:string
    email:string
    extraRoles:string list
    missingRoles:string list
  }

let getProfileRoleIds (usersProfiles: UserProfile list) (rolesPerProfile: RoleProfileRow list) (user: UserWithRoles) =
  usersProfiles
    |> List.tryFind (fun up -> up.userId = user.id) 
    |> Option.bind (fun up -> up.profileId)
    |> Option.map 
      (fun pId -> 
        rolesPerProfile 
          |> List.filter (fun rpp -> rpp.profileId = pId) 
          |> List.map (fun rpp -> rpp.roleId))
    |> Option.defaultValue []

let getProfileId usersProfiles (u: UserRoleRow) =
  usersProfiles 
    |> List.tryFind (fun up -> up.userId = u.userId) 
    |> Option.bind (fun up -> up.profileId)

let getRoles (userRoleRows: UserRoleRow list) (user: UserRoleRow) : Role list =
  userRoleRows
    |> List.filter (fun u -> u.userId = user.userId)
    |> List.map (fun u -> {id= u.RoleId; name= u.roleName})

let toDistinctUsers usersProfiles (userRoleRows: UserRoleRow list) =
  userRoleRows
    |> List.distinctBy (fun u -> u.userId)
    |> List.map (fun u -> {
      id = u.userId
      profileId = u |> getProfileId usersProfiles
      roles = (u |> getRoles userRoleRows)
      email = u.email
    })

let roleIdInList roleIds role =
  not (roleIds |> List.exists (fun rId -> role.id = rId))


let toDelinquent userProfileRows roleProfileRows (allProfiles: Profile list) (allRoles: Role list) (user: UserWithRoles) =
  let profileRoleIds = user |> getProfileRoleIds userProfileRows roleProfileRows
  let extraRoles = user.roles |> List.where (fun r -> not (profileRoleIds |> List.exists (fun pr -> r.id = pr)))
  let defaultUnknown opt = opt |> Option.defaultValue "unknown"
  let missingRoles =
   profileRoleIds
     |> List.filter (fun id -> not (user.roles |> List.exists (fun r -> r.id = id)))
     |> List.map (fun id -> allRoles |> List.find (fun r -> r.id = id))
  {
    id= user.id
    profileId=
      user.profileId
        |> Option.map (sprintf "%A")
        |> defaultUnknown
    profileName=
      option {
        let! pid = user.profileId
        let! profile = allProfiles |> List.tryFind (fun p -> p.id = pid)
        return profile.name
      } |> defaultUnknown
    email= user.email |> defaultUnknown
    extraRoles= extraRoles |> List.map (fun r -> r.name)
    missingRoles= missingRoles |> List.map (fun r -> r.name)
  }

let hasIncorrectRoles userProfileRows rolesPerProfile user =
  let profileRoleIds = user |> getProfileRoleIds userProfileRows rolesPerProfile
  let hasExtraRoles = user.roles |> List.exists (fun r -> not (profileRoleIds |> List.exists (fun pr -> r.id = pr)))
  let hasMissingRoles = profileRoleIds |> List.exists (fun prId -> not (user.roles |> List.exists (fun r -> prId = r.id)))
  hasExtraRoles || hasMissingRoles

let obtain() =
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
let main _ =
    obtain()
    printf "Press a key to end"
    Console.ReadKey() |> ignore
    0
