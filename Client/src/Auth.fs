module Auth

open Elmish
open Json
open Fable.Core
open Fable.SimpleHttp
open Fable.Core.JsInterop
open Shared

type LoginRequest = {email: string; password: string}
type RegisterRequest = {email: string; password: string}

let private is2xx (status: int) = status >= 200 && status < 300
let private bodyOrEmpty (s: string) = if isNull s then "" else s

let private parseUserObj (o: obj) : User = {
  Id = getFirst<int> o ["id"; "Id"] |> Option.defaultValue 0
  Email =
    getFirst<string> o ["email"; "Email"]
    |> Option.defaultValue ""
}

let private parseUserJson (json: string) : Result<User, exn> =
  try
    Ok (JS.JSON.parse (json) |> parseUserObj)
  with e ->
    // Add more context to the error
    let msg = sprintf "Failed to parse User JSON. Error: %s. JSON: %s" e.Message json
    Error (exn msg)

let login (req: LoginRequest) (onResult: Result<User, exn> -> 'msg) : Cmd<'msg> =
  let fetch () =
    async {
      let! res =
        Http.request "/api/auth/login"
        |> Http.method POST
        |> Http.header (Headers.contentType "application/json")
        |> Http.content (
          BodyContent.Text (
            JS.JSON.stringify (
              createObj [
                "email" ==> req.email
                "password" ==> req.password
              ]
            )
          )
        )
        |> Http.send
      let status = res.statusCode
      let body = bodyOrEmpty res.responseText
      if not (is2xx status) then
        return Error (exn (sprintf "HTTP %d: %s" status body))
      else
        return parseUserJson body
    }
  Cmd.OfAsync.either fetch () onResult (fun ex -> onResult (Error ex))

let register (req: RegisterRequest) (onResult: Result<User, exn> -> 'msg) : Cmd<'msg> =
  let fetch () =
    async {
      let! res =
        Http.request "/api/auth/register"
        |> Http.method POST
        |> Http.header (Headers.contentType "application/json")
        |> Http.content (
          BodyContent.Text (
            JS.JSON.stringify (
              createObj [
                "email" ==> req.email
                "password" ==> req.password
              ]
            )
          )
        )
        |> Http.send
      let status = res.statusCode
      let body = bodyOrEmpty res.responseText
      if not (is2xx status) then
        return Error (exn (sprintf "HTTP %d: %s" status body))
      else
        return parseUserJson body
    }
  Cmd.OfAsync.either fetch () onResult (fun ex -> onResult (Error ex))

let logout (onResult: Result<unit, exn> -> 'msg) : Cmd<'msg> =
  let fetch () =
    async {
      let! res =
        Http.request "/api/auth/logout"
        |> Http.method POST
        |> Http.send
      let status = res.statusCode
      let body = bodyOrEmpty res.responseText
      if not (is2xx status) then
        return Error (exn (sprintf "HTTP %d: %s" status body))
      else
        return Ok ()
    }
  Cmd.OfAsync.either fetch () onResult (fun ex -> onResult (Error ex))
