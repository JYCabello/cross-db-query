module DataLayer

open System
open CrossDdQueryFs

// Data models
module DataModels =
  type Role = { Id: Guid; Name: string }
  type Profile = { Id: Guid; Name: string }

  type UserRoleRow =
    { UserId: Guid
      RoleName: string
      Email: string option
      RoleId: Guid }

  type RoleProfileRow = { ProfileId: Guid; RoleId: Guid }

  type UserProfile =
    { UserId: Guid
      ProfileId: Guid option }

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
  let usersProfilesQuery =
    "SELECT FunctionProfileCode, UserId FROM UserSettings"

  [<Literal>]
  let profilesQuery =
    "SELECT Code as id, Name FROM FunctionProfile"

  [<Literal>]
  let rolesQuery = "SELECT * FROM AspNetRoles"

  let connStrings = Configuration.settings.ConnectionStrings

  type ApplicationDb = SqlDataProvider<Common.DatabaseProviderTypes.MSSQLSERVER, appConnStr>
  type CustomerDb = SqlDataProvider<Common.DatabaseProviderTypes.MSSQLSERVER, custConnStr>

  let getUserRoleRows () =
    let ctx = ApplicationDb.GetDataContext(connStrings.Application)
    query {
      for r in ctx.Dbo.AspNetRoles do
      join ur in ctx.Dbo.AspNetUserRoles on (r.Id = ur.RoleId)
      join u in ctx.Dbo.AspNetUsers on (ur.UserId = u.Id)
      select
        { RoleName = r.Name
          Email = u.Email |> Some
          UserId = u.Id |> Guid.Parse
          RoleId = ur.RoleId |> Guid.Parse }
    }
    |> Seq.toList

  let getRolesPerProfile () =
    use cmd = new SqlCommandProvider<rolesPerProfileQuery, appConnStr>(connStrings.Application)
    cmd.Execute()
    |> Seq.map (fun rpp ->
         { ProfileId = rpp.FunctionProfileCode
           RoleId = rpp.RoleId |> Guid.Parse })
    |> Seq.toList

  let getAllRoles (): Role list =
    let context = ApplicationDb.GetDataContext(connStrings.Application)
    let toRole (r: ApplicationDb.dataContext.``dbo.AspNetRolesEntity``): Role =
      { Id = r.Id |> Guid.Parse; Name = r.Name }
    query {
      for r in context.Dbo.AspNetRoles do
      select (r |> toRole)
    } |> Seq.toList

  let getProfiles () =
    use cmd = new SqlCommandProvider<profilesQuery, appConnStr>(connStrings.Application)
    cmd.Execute()
    |> Seq.map (fun r -> { Id = r.id; Name = r.Name })
    |> Seq.toList

  let getUsersProfileRows () =
    use cmd = new SqlCommandProvider<usersProfilesQuery, custConnStr>(connStrings.Customer)

    cmd.Execute()
    |> Seq.map (fun up ->
         { ProfileId = up.FunctionProfileCode
           UserId = up.UserId })
    |> Seq.toList
