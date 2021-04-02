module CrossDdQueryFs.MigrationDashboard

module R = DataLayer.Repository
let program () =
  async {
    printfn "Getting profiles"
    let! (count, profiles) = Utils.Parallel.tuple(R.settingsCount, R.profiles)
    let batchSize = 500
    let pages = count / batchSize + 1
    let parallelDegree = 15
    printfn $"Getting dashboards to set, %i{pages} batches of %i{batchSize} elements"
    let! toSet =
      List.init pages (fun page -> R.dashboardsToSet page batchSize profiles)
        |> (fun a -> Async.Parallel (a, parallelDegree))
    printfn "Setting dashboards"
    do! toSet
      |> Array.map R.setDb
      |> (fun a -> Async.Parallel (a, parallelDegree))
      |> Async.Ignore
    printfn "Dashboards set"
  }
