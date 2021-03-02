module CrossDdQueryFs.Configuration
  open System
  open System.IO
  open FSharp.Data
  open Newtonsoft.Json
  open Newtonsoft.Json.Linq

  type AppSettings = JsonProvider<"appSettings.json">

  type Environment = Development | Production
  
  let environment =
    System.Environment.GetEnvironmentVariable "ENVIRONMENT"
    |> fun s -> 
        match s with
          | null -> ""
          | _ -> s
    |> (fun s -> s.Equals("development", StringComparison.InvariantCultureIgnoreCase))
    |> function
      | true -> Environment.Development
      | false -> Environment.Production

  let getContents fileName =
    fileName
    |> File.Exists
    |> function
      | true -> File.ReadAllText fileName |> Some
      | false -> None
  
  let mergeSettings =
    let jsonMergeSettings = JsonMergeSettings()
    jsonMergeSettings.MergeArrayHandling <- MergeArrayHandling.Replace
    jsonMergeSettings
    
  let getAdditionalFileContents particle =
    sprintf "appSettings.%s.json" particle
    |> getContents
    |> Option.map JObject.Parse
    
  let settings =
    let primary = File.ReadAllText "appSettings.json" |> JObject.Parse
    let additional =
      environment
        |> function
          | Development -> "Development" |> Some
          | Production -> None
      |> Option.bind getAdditionalFileContents
    match additional with
        | Some jo -> primary.Merge(jo, mergeSettings); primary
        | None -> primary
      |> (fun jo -> jo.ToString(Formatting.None))
      |> AppSettings.Parse
            
      