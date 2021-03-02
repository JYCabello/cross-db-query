module DataLayer
open System
open CrossDdQueryFs

// Data models
module DataModels =
  type Role = {id:Guid; name:string}
  type Profile = {id:Guid; name:string}
  type UserRoleRow = {userId:Guid; roleName:string; email:string option; RoleId:Guid}
  type RoleProfileRow = {profileId: Guid; roleId: Guid}
  type UserProfile = {userId: Guid; profileId: Guid option}

module Repository =
  open DataModels
  open FSharp.Data
    
  [<Literal>]
  let connectionStringApplication = 
    "Server=(LocalDB)\MSSQLLocalDB;Integrated Security=true;initial catalog=box-application-localdb;"
    
  [<Literal>]
  let connectionStringCustomer = 
    "Server=(LocalDB)\MSSQLLocalDB;Integrated Security=true;initial catalog=box-application-localdb;"

  [<Literal>]
  let userRolesQuery ="
  SELECT r.[Name] as [RoleName], u.Email, u.Id as UserId, r.Id as RoleId
  FROM AspNetRoles r
  INNER JOIN AspNetUserRoles ur ON ur.RoleId = r.Id
  INNER JOIN AspNetUsers u ON u.Id = ur.UserId
  "

  [<Literal>]
  let rolesPerProfileQuery = "SELECT * FROM RolePerFunctionProfile"

  [<Literal>]
  let usersProfilesQuery = "SELECT FunctionProfileCode, UserId FROM UserSettings"

  [<Literal>]
  let profilesQuery = "SELECT Code as id, Name FROM FunctionProfile"

  [<Literal>]
  let rolesQuery = "SELECT * FROM AspNetRoles"

  let connStrings = Configuration.settings.ConnectionStrings
  
  let getUserRoleRows() =
    use cmd = new SqlCommandProvider<userRolesQuery, connectionStringApplication>(connStrings.Application)
    cmd.Execute() 
      |> Seq.map (fun u -> {userId = u.UserId |> Guid.Parse; roleName = u.RoleName; email = u.Email; RoleId = u.RoleId |> Guid.Parse})
      |> Seq.toList

  let getRolesPerProfile() =
    use cmd = new SqlCommandProvider<rolesPerProfileQuery, connectionStringApplication>(connStrings.Application)
    cmd.Execute()
      |> Seq.map (fun rpp -> {profileId = rpp.FunctionProfileCode; roleId = rpp.RoleId |> Guid.Parse})
      |> Seq.toList

  let getAllRoles() =
    use cmd = new SqlCommandProvider<rolesQuery, connectionStringApplication>(connStrings.Application)
    let toRole (r: SqlCommandProvider<rolesQuery, connectionStringApplication>.Record) :Role = {id=r.Id |> Guid.Parse; name=r.Name}
    cmd.Execute()
      |> Seq.map toRole
      |> Seq.toList

  let getProfiles() =
    use cmd = new SqlCommandProvider<profilesQuery, connectionStringApplication>(connStrings.Application)
    cmd.Execute()
      |> Seq.map (fun r -> {id=r.id; name=r.Name})
      |> Seq.toList

  let getUsersProfileRows() =
    use cmd = new SqlCommandProvider<usersProfilesQuery, connectionStringCustomer>(connStrings.Customer)
    cmd.Execute()
      |> Seq.map (fun up -> {profileId = up.FunctionProfileCode; userId = up.UserId})
      |> Seq.toList