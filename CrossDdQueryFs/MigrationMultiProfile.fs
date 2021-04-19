module CrossDdQueryFs.MigrationMultiProfile

open System
open System.IO
open Utils

module R = DataLayer.Repository

let program () =
  async {
    let! allProfiles, usersProfiles = Parallel.tuple (R.profiles(), R.usersProfilesTotal())
    let dateFormat = "yyyyMMddHHmmss"
    File.WriteAllLines(
      $"usersProfiles%s{DateTime.Now.ToString(dateFormat)}.csv",
      List.map (sprintf "%s") (usersProfiles |> MapUtils.toLines)
    )
    let! _ = R.setUsersProfiles usersProfiles allProfiles
    return ()
  }
