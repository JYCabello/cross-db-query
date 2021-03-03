module CrossDdQueryFs.Configuration
  open System.IO
  open FSharp.Data
  open Newtonsoft.Json
  open Newtonsoft.Json.Linq

  type AppSettings = JsonProvider<"appSettings.json">

  let getContents fileName =
    if File.Exists fileName then File.ReadAllText fileName |> Some else None
      
  let envSettings =
    System.Environment.GetEnvironmentVariable "ENVIRONMENT"
    |> (fun s -> if System.String.IsNullOrWhiteSpace s then None else s.ToLowerInvariant() |> Some)
    |> Option.map (sprintf "appSettings.%s.json")
    |> Option.bind getContents
    |> Option.map JObject.Parse
  
  let mergeSettings =
    let jsonMergeSettings = JsonMergeSettings()
    jsonMergeSettings.MergeArrayHandling <- MergeArrayHandling.Replace
    jsonMergeSettings
    
  let settings =
    let primary = "appSettings.json" |> File.ReadAllText |> JObject.Parse
    match envSettings with
        | Some jo -> primary.Merge(jo, mergeSettings); primary
        | None -> primary
      |> (fun jo -> jo.ToString(Formatting.None))
      |> AppSettings.Parse
            
      