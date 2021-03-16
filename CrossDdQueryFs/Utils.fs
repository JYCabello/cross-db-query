﻿module Utils

module OptionUtils =
  type OptionBinding() =
    member this.Bind(option, fn) =
      match option with
        | None -> None
        | Some a -> fn a
    member this.Return(x) = Some x
    member this.ReturnFrom(option) = option

  let option = OptionBinding()
  
module ParallelUtils =
  let parallelTuple2 (async1, async2) =
    async {
      let! child1 = Async.StartChild (async1())
      let! child2 = Async.StartChild (async2())
      let! result1 = child1
      let! result2 = child2
      return (result1, result2)
    }

  let parallelTuple3 (async1, async2, async3) =
    async {
      let! childTuple = parallelTuple2 (async1, async2) |> Async.StartChild
      let! child3 = Async.StartChild (async3())
      let! (result1, result2) = childTuple
      let! result3 = child3
      return (result1, result2, result3)
    }

  let parallelTuple4 (async1, async2, async3, async4) =
    async {
      let! childTuple = parallelTuple3 (async1, async2, async3) |> Async.StartChild
      let! child4 = Async.StartChild (async4())
      let! (result1, result2, result3) = childTuple
      let! result4 = child4
      return (result1, result2, result3, result4)
    }

  let parallelTuple5 (async1, async2, async3, async4, async5) =
    async {
      let! childTuple = parallelTuple4 (async1, async2, async3, async4) |> Async.StartChild
      let! child5 = Async.StartChild (async5())
      let! (result1, result2, result3, result4) = childTuple
      let! result5 = child5
      return (result1, result2, result3, result4, result5)
    }

  let parallelTuple6 (async1, async2, async3, async4, async5, async6) =
    async {
      let! childTuple = parallelTuple5 (async1, async2, async3, async4, async5) |> Async.StartChild
      let! child6 = Async.StartChild (async6())
      let! (result1, result2, result3, result4, result5) = childTuple
      let! result6 = child6
      return (result1, result2, result3, result4, result5, result6)
    }

module MapUtils =
  let emptyMap() = Map<'a, 'b list> []

  let appendTo (key, value) (d: Map<'a,'b list>) =
    let current =
      match d.TryGetValue(key) with
        | true, value -> value
        | false, _ -> []
    d.Add(key, value :: current)

  let collectToMap (l: ('a * 'b) list) =
    l |> List.fold (fun acc t -> appendTo t acc) (emptyMap())

  let chunkMap size =
    Map.fold (fun acc key value -> (key, value) :: acc) [] 
    >> List.chunkBySize size
    >> List.map (fun ll -> ll |> List.fold (fun (acc: Map<'a, 'b list>) -> acc.Add) (emptyMap()))
    
  let toLines map =
    let toDenormalizedTuples key = List.fold (fun acc value -> (key, value) :: acc) []
    map
    |> Map.fold (fun acc key values -> values |> toDenormalizedTuples key |> List.append acc) []
    |> List.map (fun (key, value) -> (key.ToString(), value.ToString()))
    |> (fun l -> ("UserID", "ProfileID") :: l)
    |> List.map (fun (key, value) -> $"{key}, {value}")
    
module ListUtils =
  let appendTupleList tupleList =
    tupleList
    |> Seq.fold
      (fun (accA, accB, accC) (elmA, elmB, elmC) ->
        ((elmA |> List.append accA), (elmB |> List.append accB), (elmC |> List.append accC))
      )
      ([],[],[])