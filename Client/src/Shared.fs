module ClientShared

open Shared

let asUnexpected wrap (ex: exn) =
  ex.Message
  |> Unexpected
  |> Error
  |> wrap
