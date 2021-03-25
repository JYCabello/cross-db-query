module DataLayer

open System
open CrossDdQueryFs
open Utils

// Data models
module DataModels =
  type Role = { Id: Guid; Name: string }
  type Profile = { ID: Guid; Name: string; DefaultDashboard: int }
  type UserRoleRow = { UserId: Guid; RoleName: string; Email: string option; RoleId: Guid }
  type RoleProfileRow = { ProfileId: Guid; RoleId: Guid }
  type UserProfileRow = { UserId: Guid; PossibleProfileId: Guid option }
  type UserSettings = { UserId: Guid; Dashboard: int option; ProfileId: Guid option }
  type UserSetDashBoard = { UserId: Guid; Dashboard: int }

module Repository =
  open DataModels
  open FSharp.Data
  open FSharp.Data.Sql

  [<Literal>]
  let appConnStr =
    "Server=(LocalDB)\\MSSQLLocalDB;Integrated Security=true;initial catalog=box-application-localdb;"

  [<Literal>]
  let custConnStr =
    "Server=(LocalDB)\\MSSQLLocalDB;Integrated Security=true;initial catalog=box-customer-localdb;"

  let connStrs = Configuration.settings.ConnectionStrings

  type ApplicationDb =
    SqlDataProvider<
      DatabaseVendor = Common.DatabaseProviderTypes.MSSQLSERVER,
      ConnectionString = appConnStr,
      UseOptionTypes = true>

  let appCtx () = ApplicationDb.GetDataContext(connStrs.Application)

  type CustomerDb =
    SqlDataProvider<
      DatabaseVendor = Common.DatabaseProviderTypes.MSSQLSERVER,
      ConnectionString = custConnStr,
      UseOptionTypes = true>

  let custCtx () = CustomerDb.GetDataContext(connStrs.Customer)

  type RolesEntity = ApplicationDb.dataContext.``dbo.AspNetRolesEntity``
  let toRole (r: RolesEntity): Role = { Id = r.Id |> Guid.Parse; Name = r.Name }

  let usersRoles () =
    query {
      for r in appCtx().Dbo.AspNetRoles do
      for ur in r.``dbo.AspNetUserRoles by Id`` do
      for u in ur.``dbo.AspNetUsers by Id`` do
      select
        { RoleName = r.Name
          Email = u.Email
          UserId = u.Id |> Guid.Parse
          RoleId = ur.RoleId |> Guid.Parse }
    } |> List.executeQueryAsync

  let rolesPerProfile () =
    query {
      for rpp in appCtx().Dbo.RolePerFunctionProfile do
      select { ProfileId = rpp.FunctionProfileCode; RoleId = rpp.RoleId |> Guid.Parse }
    } |> List.executeQueryAsync

  let roles () =
    query { for r in appCtx().Dbo.AspNetRoles do select (r |> toRole) } |> List.executeQueryAsync

  let profiles () =
    query { for r in appCtx().Dbo.FunctionProfile do select { ID = r.Code; Name = r.Name; DefaultDashboard = r.Dashboard } }
    |> List.executeQueryAsync

  let usersProfilesBySettings () =
    query {
      for up in custCtx().Dbo.UserSettings do select { PossibleProfileId = up.FunctionProfileCode; UserId = up.UserId }
    } |> List.executeQueryAsync

  type UserWithProfiles = { UserId: Guid; ProfileIDs: Guid list }
  
  let usersProfilesTotal () =
    let ctx = custCtx()
    async {
      let! pairsFromProfile =
        query { for r in ctx.Dbo.UsersApplicationFunctionProfiles do select (r.UserId, r.FunctionProfileCode) }
          |> List.executeQueryAsync
      let! pairsFromSettings =
        query {
          for up in ctx.Dbo.UserSettings do
          where (up.FunctionProfileCode.IsSome)
          select (up.UserId, up.FunctionProfileCode.Value)
        } |> List.executeQueryAsync
      return pairsFromProfile
             |> List.append pairsFromSettings
             |> List.distinct
             |> MapUtils.collectToMap
    }
  open System.Linq
  
  let setUsersProfiles (userProfiles: Map<Guid, Guid list>) allProfiles =
    let setUsersProfilesChunk (userProfilesChunk: Map<Guid, Guid list>) =
      async {
        let ctx = custCtx()
        let toRelations userID profileIDs =
            profileIDs
            |> List.fold (fun acc profileID -> ctx.Dbo.UsersApplicationFunctionProfiles.``Create(FunctionProfileCode, UserId)``(profileID, userID) :: acc) []
        let userIDs = userProfilesChunk |> Map.fold (fun acc key _ -> key :: acc) [] |> List.toArray
        let! settings =
          query {
            for s in ctx.Dbo.UserSettings do
            where (userIDs.Contains(s.UserId))
          } |> List.executeQueryAsync
        settings |> List.iter (fun s -> s.FunctionProfileCode <- None)
        let! existingRelations =
          query {
            for uafp in ctx.Dbo.UsersApplicationFunctionProfiles do
            where (userIDs.Contains(uafp.UserId))
          } |> List.executeQueryAsync
        let onlyExistingProfiles = List.filter (fun pID -> allProfiles |> List.exists (fun p -> p.ID = pID))
        let onlyNotYetAssignedProfiles userID profileIDs =
          existingRelations
            |> List.exists (fun r -> r.UserId = userID && profileIDs |> List.exists (fun pid -> pid = r.FunctionProfileCode))
            |> not
        let newRelations =
          userProfilesChunk
            |> Map.map (fun _ pIDs -> pIDs |> onlyExistingProfiles)
            |> Map.filter onlyNotYetAssignedProfiles
            |> Map.fold (fun acc userID profileIDs -> acc |> List.append (toRelations userID profileIDs) ) []
        do! ctx.SubmitUpdatesAsync()
        printfn "Completed one transaction"
        return (settings, existingRelations, newRelations |> List.map (fun r -> (r.UserId, r.FunctionProfileCode)))
      }
    async {
      let chunkSize = 750
      printfn "Total of %i, iterating on %i batches" userProfiles.Count (userProfiles.Count / chunkSize)
      let! results =
        userProfiles
        |> MapUtils.chunkMap chunkSize
        |> List.map setUsersProfilesChunk
        |> (fun c -> Async.Parallel (c, 10))
      return results |> ListUtils.appendTupleList
    }
    
  let settingsCount () =
    async { return query { for s in custCtx().Dbo.UserSettings do count } } 
    
  let dashboardsToSet page batchSize allProfiles =
    async {
      let ctx = custCtx()
      let! settings =
        query {
          for s in ctx.Dbo.UserSettings do
          sortBy (s.UserId)
          skip (page * batchSize)
          take batchSize
        } |> List.executeQueryAsync

      let! profilesLinked =
        let userIDs = settings |> List.map (fun s -> s.UserId) |> List.toArray
        query {
          for uafp in ctx.Dbo.UsersApplicationFunctionProfiles do
          where (userIDs.Contains(uafp.UserId))
        } |> List.executeQueryAsync

      let toUserSettings (s: CustomerDb.dataContext.``dbo.UserSettingsEntity``) =
        let profileID =
          profilesLinked
            |> List.tryFind (fun p -> p.UserId = s.UserId)
            |> Option.map (fun p -> p.FunctionProfileCode)
        { UserId = s.UserId
          Dashboard = s.Dashboard
          ProfileId = profileID }

      let withDashboardID (s: UserSettings) =
        s.ProfileId
        |> Option.bind
             (fun pId ->
              allProfiles
                |> List.tryFind (fun (p: Profile) -> p.ID = pId)
                |> Option.map (fun p -> { UserId = s.UserId; Dashboard = p.DefaultDashboard })
            )

      return
          settings
            |> List.map toUserSettings
            |> List.filter (fun s -> s.Dashboard.IsNone && s.ProfileId.IsSome)
            |> List.choose withDashboardID
    }
  
  let setDb (dashboardsToSet: UserSetDashBoard list) =
    let ctx = custCtx()
    let ids = dashboardsToSet |> List.map (fun dts -> dts.UserId) |> List.toArray
    async {
      let! settings =
        query {
          for s in ctx.Dbo.UserSettings do
          where (ids.Contains(s.UserId))
        } |> List.executeQueryAsync

      dashboardsToSet
        |> List.choose
             (fun dts ->
                settings
                  |> List.tryFind (fun s -> s.UserId = dts.UserId)
                  |> Option.map (fun s -> (dts, s))
             )
        |> List.iter (fun (dts, s) -> s.Dashboard <- dts.Dashboard |> Some)

      return! ctx.SubmitUpdatesAsync ()
    }

  let setDashboards () =
    let batchSize = 500
    let rec go page allProfiles =
      let ctx = custCtx()
      async {
        let! settings =
          query {
            for s in ctx.Dbo.UserSettings do
            sortBy (s.UserId)
            skip (page * batchSize)
            take batchSize
          } |> List.executeQueryAsync
        let! profilesLinked = ctx.Dbo.UsersApplicationFunctionProfiles |> List.executeQueryAsync
        let withProfileNoDashboard =
          settings
            |> List.map
              (fun s ->
                let profileID =
                  profilesLinked
                    |> List.tryFind (fun p -> p.UserId = s.UserId)
                    |> Option.map (fun p -> p.FunctionProfileCode)
                { UserId = s.UserId
                  Dashboard = s.Dashboard
                  ProfileId = profileID }
              )
            |> List.filter (fun s -> s.Dashboard.IsNone && s.ProfileId.IsSome)
        withProfileNoDashboard
          |> List.iter
            (fun s ->
              let dashboard =
                s.ProfileId
                |> Option.bind (fun pid -> allProfiles |> List.tryFind (fun p -> p.ID = pid))
                |> Option.map (fun p -> p.DefaultDashboard)
                |> Option.defaultValue 0
              let setting = settings |> List.find (fun st -> st.UserId = s.UserId)
              setting.Dashboard <- dashboard |> Some
            )
        let recordsToUpdate = ctx.GetUpdates () |> List.length
        do! ctx.SubmitUpdatesAsync ()
        printfn $"Completed batch number {page + 1} with {recordsToUpdate} update(s) done."
        return! if (settings.Count () < batchSize) then (async { () }) else (go (page + 1) allProfiles)
      }
    async {
      let! allProfiles = profiles ()
      return! go 0 allProfiles
    }
    