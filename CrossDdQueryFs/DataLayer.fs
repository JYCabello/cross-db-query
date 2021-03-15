module DataLayer

open System
open CrossDdQueryFs

// Data models
module DataModels =
  type Role = { Id: Guid; Name: string }
  type Profile = { Id: Guid; Name: string }
  type UserRoleRow = { UserId: Guid; RoleName: string; Email: string option; RoleId: Guid }
  type RoleProfileRow = { ProfileId: Guid; RoleId: Guid }
  type UserProfile = { UserId: Guid; ProfileId: Guid option }

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
    query { for r in appCtx().Dbo.FunctionProfile do select { Id = r.Code; Name = r.Name } }
    |> List.executeQueryAsync

  let usersProfilesBySettings () =
    query {
      for up in custCtx().Dbo.UserSettings do select { ProfileId = up.FunctionProfileCode; UserId = up.UserId }
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
             |> Utils.collectToMap
    }
  open System.Linq
  
  let setUsersProfiles (up: Map<Guid, Guid list>) =
    let setUsersProfilesChunk (userProfiles: Map<Guid, Guid list>) =
      let ctx = custCtx()
      let toRelations userID profileIDs =
          profileIDs
          |> List.fold (fun acc profileID -> ctx.Dbo.UsersApplicationFunctionProfiles.``Create(FunctionProfileCode, UserId)``(profileID, userID) :: acc) []
      async {
        let userIDs = userProfiles |> Map.fold (fun acc key _ -> key :: acc) [] |> List.toArray
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
        existingRelations |> List.iter (fun r -> r.Delete())
        let! _ = ctx.SubmitUpdatesAsync()
        let newRelations = userProfiles |> Map.fold (fun acc userID profileIDs -> acc |> List.append (toRelations userID profileIDs) ) []
        let! _ = ctx.SubmitUpdatesAsync()
        return (settings, existingRelations, newRelations)
      }
    async {
      let! results =
        up
        |> Utils.chunkMap 500
        |> List.map (setUsersProfilesChunk)
        |> Async.Parallel
      return
        results
        |> Seq.fold
          (fun acc elm ->
            let (elmA, elmB, elmC) = elm
            let (accA, accB, accC) = acc
            ((elmA |> List.append accA), (elmB |> List.append accB), (elmC |> List.append accC))
          )
          ([],[],[])
    }