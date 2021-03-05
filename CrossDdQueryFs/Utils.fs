module Utils

type OptionBinding() =
  member this.Bind(option, fn) =
    match option with
      | None -> None
      | Some a -> fn a
  member this.Return(x) = Some x
  member this.ReturnFrom(option) = option

let option = OptionBinding()

let parallelTuple5 (async1, async2, async3, async4, async5) =
  async {
    let! child1 = Async.StartChild async1
    let! child2 = Async.StartChild async2
    let! child3 = Async.StartChild async3
    let! child4 = Async.StartChild async4
    let! child5 = Async.StartChild async5
    let! result1 = child1
    let! result2 = child2
    let! result3 = child3
    let! result4 = child4
    let! result5 = child5
    return (result1, result2, result3, result4, result5)
  }