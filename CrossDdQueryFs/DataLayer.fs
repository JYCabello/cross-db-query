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

  [<Literal>]
  let rolesPerProfileQuery = "SELECT * FROM RolePerFunctionProfile"

  [<Literal>]
  let usersProfilesQuery = "SELECT FunctionProfileCode, UserId FROM UserSettings"

  [<Literal>]
  let profilesQuery = "SELECT Code as id, Name FROM FunctionProfile"

  let connStrings = Configuration.settings.ConnectionStrings

  type ApplicationDb =
    SqlDataProvider<
      DatabaseVendor = Common.DatabaseProviderTypes.MSSQLSERVER,
      ConnectionString = appConnStr,
      UseOptionTypes = true>
  let getAppContext () = ApplicationDb.GetDataContext(connStrings.Application)
  type RolesEntity = ApplicationDb.dataContext.``dbo.AspNetRolesEntity``
  type CustomerDb =
    SqlDataProvider<
      DatabaseVendor = Common.DatabaseProviderTypes.MSSQLSERVER,
      ConnectionString = custConnStr,
      UseOptionTypes = true>
  let getCustContext () = CustomerDb.GetDataContext(connStrings.Customer)

  let getUserRoleRows () =
    let ctx = getAppContext ()

    query {
      for r in ctx.Dbo.AspNetRoles do
      for ur in r.``dbo.AspNetUserRoles by Id`` do
      for u in ur.``dbo.AspNetUsers by Id`` do
      select
        { RoleName = r.Name
          Email = u.Email
          UserId = u.Id |> Guid.Parse
          RoleId = ur.RoleId |> Guid.Parse }
    } |> Seq.toList

  let getRolesPerProfile () =
    let context = getAppContext()
    query {
      for rpp in context.Dbo.RolePerFunctionProfile do
      select { ProfileId = rpp.FunctionProfileCode; RoleId = rpp.RoleId |> Guid.Parse }
    } |> Seq.toList

  let getAllRoles () =
    let context = getAppContext()
    let toRole (r: RolesEntity): Role = { Id = r.Id |> Guid.Parse; Name = r.Name }
    query {
      for r in context.Dbo.AspNetRoles do
      select (r |> toRole)
    }
    |> Seq.toList

  let getProfiles () =
    use cmd = new SqlCommandProvider<profilesQuery, appConnStr>(connStrings.Application)
    cmd.Execute() |> Seq.map (fun r -> { Id = r.id; Name = r.Name }) |> Seq.toList

  let getUsersProfileRows () =
    let context = getCustContext()
    query {
      for up in context.Dbo.UserSettings do
      select { ProfileId = up.FunctionProfileCode; UserId = up.UserId }
    } |> Seq.toList
