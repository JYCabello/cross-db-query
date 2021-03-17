module CrossDdQueryFs.Migration

open System
open System.IO
open Utils

module R = DataLayer.Repository

let program () =
  async {
    let! (allProfiles, usersProfiles) = Parallel.Utils.parallelTuple (R.profiles, R.usersProfilesTotal)
    File.WriteAllLines($"usersProfiles{DateTime.Now : yyyyMMddHHmmss}.csv", List.map (sprintf "%s") (usersProfiles |> MapUtils.toLines))
    let! _ = R.setUsersProfiles usersProfiles allProfiles
    return ()
  }