module Shared

open System

[<CLIMutable>]
type User = {Id: int; Email: string}

type Theme =
  | Light
  | Dark
