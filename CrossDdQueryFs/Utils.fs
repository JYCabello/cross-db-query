module Utils

type OptionBinding() =
  member this.Bind(option, fn) =
    match option with
      | None -> None
      | Some a -> fn a
  member this.Return(x) = Some x
  member this.ReturnFrom(option) = option

let option = OptionBinding()

open Microsoft.FSharp.Reflection

let GetUnionCaseName (x:'a) = 
    match FSharpValue.GetUnionFields(x, typeof<'a>) with
    | case, _ -> case.Name

let GetUnionCaseNames<'ty> = 
    FSharpType.GetUnionCases(typeof<'ty>) |> Array.map (fun info -> info.Name)