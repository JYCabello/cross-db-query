module CrossDdQueryFs.Configuration
  open System.IO
  open FSharp.Data
  open Newtonsoft.Json
  open Newtonsoft.Json.Linq

  type AppSettings = JsonProvider<"appSettings.json">

  let parseFile = File.ReadAllText >> JObject.Parse

  let envSettings =
    System.Environment.GetEnvironmentVariable "ENVIRONMENT"
      |> (fun s -> if System.String.IsNullOrWhiteSpace s then None else s.ToLowerInvariant() |> Some)
      |> Option.map (sprintf "appSettings.%s.json")
      |> Option.bind (fun file -> if File.Exists file then parseFile file |> Some else None)

  let mergeSettings =
    let jsonMergeSettings = JsonMergeSettings()
    jsonMergeSettings.MergeArrayHandling <- MergeArrayHandling.Replace
    jsonMergeSettings

  let settings =
    envSettings
      |> Option.fold (fun (acc: JObject) (jo:JObject) -> acc.Merge(jo, mergeSettings); acc) (parseFile "appSettings.json")
      |> fun jo -> jo.ToString(Formatting.None)
      |> AppSettings.Parse
