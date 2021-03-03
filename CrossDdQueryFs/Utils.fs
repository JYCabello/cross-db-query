module Utils

type OptionBinding() =
  member this.Bind(option, fn) =
    match option with
      | None -> None
      | Some a -> fn a
  member this.Return(x) = Some x
  member this.ReturnFrom(option) = option

let option = OptionBinding()
