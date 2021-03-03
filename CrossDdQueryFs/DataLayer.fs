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

  let getUserRoleRows () =
    query {
      for r in appCtx().Dbo.AspNetRoles do
      for ur in r.``dbo.AspNetUserRoles by Id`` do
      for u in ur.``dbo.AspNetUsers by Id`` do
      select
        { RoleName = r.Name
          Email = u.Email
          UserId = u.Id |> Guid.Parse
          RoleId = ur.RoleId |> Guid.Parse }
    } |> Seq.toList

  let getRolesPerProfile () =
    query {
      for rpp in appCtx().Dbo.RolePerFunctionProfile do
      select { ProfileId = rpp.FunctionProfileCode; RoleId = rpp.RoleId |> Guid.Parse }
    } |> Seq.toList

  let getAllRoles () =
    query { for r in appCtx().Dbo.AspNetRoles do select (r |> toRole) } |> Seq.toList

  let getProfiles () =
    query { for r in appCtx().Dbo.FunctionProfile do select { Id = r.Code; Name = r.Name } }
    |> Seq.toList

  let getUsersProfileRows () =
    query {
      for up in custCtx().Dbo.UserSettings do select { ProfileId = up.FunctionProfileCode; UserId = up.UserId }
    } |> Seq.toList
