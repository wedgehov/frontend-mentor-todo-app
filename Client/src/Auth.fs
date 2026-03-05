module Auth

open Elmish
open Shared
open ClientShared

let appErrorToMessage (error: AppError) =
  match error with
  | Unauthorized -> "Unauthorized."
  | NotFound -> "Not found."
  | Conflict -> "A user with that email already exists."
  | InvalidCredentials -> "Invalid credentials."
  | ValidationError msg -> msg
  | Unexpected msg -> msg

let login (req: LoginRequest) (onResult: Result<User, AppError> -> 'msg) : Cmd<'msg> =
  Cmd.OfAsync.either
    ApiClient.AuthApi.Login
    req
    onResult
    (asUnexpected onResult)

let register (req: RegisterRequest) (onResult: Result<User, AppError> -> 'msg) : Cmd<'msg> =
  Cmd.OfAsync.either
    ApiClient.AuthApi.Register
    req
    onResult
    (asUnexpected onResult)

let logout (onResult: Result<unit, AppError> -> 'msg) : Cmd<'msg> =
  Cmd.OfAsync.either
    ApiClient.AuthApi.Logout
    ()
    onResult
    (asUnexpected onResult)
