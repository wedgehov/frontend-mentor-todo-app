module Routing

open Elmish.UrlParser

type Page =
  | TodosPage of int
  | LoginPage
  | RegisterPage

type RouteData = {Page: Page; TodoFilter: TodosPage.Filter option}

let private parseTodoFilter (value: string) =
  match value.ToLowerInvariant () with
  | "all" -> Some TodosPage.All
  | "active" -> Some TodosPage.Active
  | "completed" -> Some TodosPage.Completed
  | _ -> None

let private todoFilterToQueryValue (filter: TodosPage.Filter) =
  match filter with
  | TodosPage.All -> "all"
  | TodosPage.Active -> "active"
  | TodosPage.Completed -> "completed"

let private todoFilterParam = customParam "filter" (Option.bind parseTodoFilter)

let toHashPath (page: Page) =
  match page with
  | LoginPage -> "#/login"
  | RegisterPage -> "#/register"
  | TodosPage userId -> $"#/user/{userId}/todos"

let toTodosHashPath (userId: int) (filter: TodosPage.Filter) =
  let basePath = toHashPath (TodosPage userId)
  match filter with
  | TodosPage.All -> basePath
  | _ -> $"{basePath}?filter={todoFilterToQueryValue filter}"

let defaultRoute = {Page = LoginPage; TodoFilter = None}

let private pageParser =
  oneOf [
    map (fun f -> {Page = LoginPage; TodoFilter = f}) (top <?> todoFilterParam)
    map
      (fun userId filter -> {Page = TodosPage userId; TodoFilter = filter})
      ((s "user" </> i32 </> s "todos") <?> todoFilterParam)
    map (fun f -> {Page = LoginPage; TodoFilter = f}) ((s "login") <?> todoFilterParam)
    map (fun f -> {Page = RegisterPage; TodoFilter = f}) ((s "register") <?> todoFilterParam)
  ]

let urlParser: Browser.Types.Location -> RouteData option = parseHash pageParser
