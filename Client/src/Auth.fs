module Auth

open Elmish
open Shared

let appErrorToMessage (error: AppError) =
  match error with
  | Unauthorized -> "Unauthorized."
  | NotFound -> "Not found."
  | Conflict -> "A user with that email already exists."
  | InvalidCredentials -> "Invalid credentials."
  | ValidationError msg -> msg
  | Unexpected msg -> msg

let private toUnexpected onResult (ex: exn) =
  onResult (Error (Unexpected ex.Message))

let login (req: LoginRequest) (onResult: Result<User, AppError> -> 'msg) : Cmd<'msg> =
  Cmd.OfAsync.either
    ApiClient.AuthApi.Login
    req
    onResult
    (toUnexpected onResult)

let register (req: RegisterRequest) (onResult: Result<User, AppError> -> 'msg) : Cmd<'msg> =
  Cmd.OfAsync.either
    ApiClient.AuthApi.Register
    req
    onResult
    (toUnexpected onResult)

let logout (onResult: Result<unit, AppError> -> 'msg) : Cmd<'msg> =
  Cmd.OfAsync.either
    ApiClient.AuthApi.Logout
    ()
    onResult
    (toUnexpected onResult)
