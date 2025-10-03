module Json

open Fable.Core
open Fable.Core.JsInterop

let inline isUndef (v: obj) : bool = emitJsExpr v "($0 === undefined)"
let inline tryUnbox<'T> (v: obj) : 'T option = try Some (unbox<'T> v) with _ -> None

let inline getFirst<'T> (o: obj) (names: string list) : 'T option =
  if isNull o then None else
  names
  |> List.tryPick (fun k ->
      let v : obj = o?(k)
      if isNull v || isUndef v then None else tryUnbox<'T> v)