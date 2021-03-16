module CrossDdQueryFs.Migration

open System.IO
open Utils

module R = DataLayer.Repository

let program () =
  async {
    let! (allProfiles, usersProfiles) = ParallelUtils.parallelTuple2 (R.profiles, R.usersProfilesTotal)
    //File.WriteAllLines("usersProfiles.txt", List.map (sprintf "%A") usersProfiles)
    return ()
  }