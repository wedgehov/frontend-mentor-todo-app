module App

open Elmish
open Elmish.React
open Elmish.Navigation
open Feliz
open Browser.Dom
open Shared
open Routing

type Theme =
  | Light
  | Dark

let private themeStorageKey = "todo.theme"

let private parseStoredTheme (value: string) =
  match value.ToLowerInvariant () with
  | "light" -> Some Light
  | "dark" -> Some Dark
  | _ -> None

let private loadSavedTheme () =
  try
    window.localStorage.getItem themeStorageKey
    |> Option.ofObj
    |> Option.bind parseStoredTheme
    |> Option.defaultValue Dark
  with _ ->
    Dark

let private persistTheme (theme: Theme) =
  let value =
    match theme with
    | Light -> "light"
    | Dark -> "dark"

  try
    window.localStorage.setItem (themeStorageKey, value)
  with _ ->
    ()

type Model = {
  Page: Page
  User: User option
  AuthChecked: bool
  Theme: Theme
  LogoutError: string option
  IsLoggingOut: bool
  Todos: TodosPage.Model
  Login: LoginPage.Model
  Register: RegisterPage.Model
}

// Main messages
type Msg =
  | TodosMsg of TodosPage.Msg
  | LoginMsg of LoginPage.Msg
  | RegisterMsg of RegisterPage.Msg
  | InitAuthResult of Result<User, AppError>
  | ThemeLoaded of Theme
  | ThemePersisted
  | LogoutResult of Result<unit, AppError>
  | RequestLogout
  | ToggleTheme

let private routeForPage (page: Page) (todosFilter: TodosPage.Filter) =
  match page with
  | TodosPage userId -> toTodosHashPath userId todosFilter
  | _ -> toHashPath page

let private guardPageForUser (requestedPage: Page) (user: User option) =
  match requestedPage, user with
  | TodosPage _, None -> LoginPage
  | TodosPage requestedUserId, Some currentUser when requestedUserId <> currentUser.Id -> TodosPage currentUser.Id
  | TodosPage _, Some currentUser -> TodosPage currentUser.Id
  | (LoginPage | RegisterPage), Some currentUser -> TodosPage currentUser.Id
  | requestedPage, None -> requestedPage

let urlUpdate (result: RouteData option) (model: Model) : Model * Cmd<Msg> =
  let requestedRoute = result |> Option.defaultValue defaultRoute
  let requestedPage = requestedRoute.Page
  let filterFromUrl =
    requestedRoute.TodoFilter
    |> Option.defaultValue TodosPage.All
  let actualPage =
    if model.AuthChecked then
      guardPageForUser requestedPage model.User
    else
      requestedPage

  // When navigating to a page, reset its specific state to clear old form data.
  let newModel =
    match actualPage with
    | LoginPage -> {model with Page = LoginPage; Login = LoginPage.init ()}
    | RegisterPage -> {model with Page = RegisterPage; Register = RegisterPage.init ()}
    | TodosPage _ -> {model with Page = actualPage; Todos = {model.Todos with Filter = filterFromUrl}}

  let shouldRedirect =
    model.AuthChecked
    && (result.IsNone || requestedPage <> actualPage)
  let redirectCmd =
    if shouldRedirect then
      Navigation.newUrl (routeForPage actualPage filterFromUrl)
    else
      Cmd.none

  let loadTodosCmd =
    match actualPage, model.User, model.AuthChecked with
    | TodosPage _, Some _, true -> Cmd.ofMsg (TodosMsg TodosPage.LoadTodos)
    | _ -> Cmd.none

  newModel, Cmd.batch [redirectCmd; loadTodosCmd]

// Init
let init (result: Option<RouteData>) : Model * Cmd<Msg> =
  let route = result |> Option.defaultValue defaultRoute
  let page = route.Page
  let (todosModel, todosCmd) = TodosPage.init ()
  let model = {
    Page = page
    User = None
    AuthChecked = false
    Theme = Dark
    LogoutError = None
    IsLoggingOut = false
    Todos = todosModel
    Login = LoginPage.init ()
    Register = RegisterPage.init ()
  }
  let (newModel, routeCmd) = urlUpdate (Some route) model

  newModel,
  Cmd.batch [
    todosCmd |> Cmd.map TodosMsg
    routeCmd
    Auth.getCurrentUser InitAuthResult
    Cmd.OfFunc.perform loadSavedTheme () ThemeLoaded
  ]

// Update
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
  match msg with
  | ToggleTheme ->
    let nextTheme =
      match model.Theme with
      | Light -> Dark
      | Dark -> Light

    {model with Theme = nextTheme}, Cmd.OfFunc.perform persistTheme nextTheme (fun _ -> ThemePersisted)
  | ThemeLoaded theme -> {model with Theme = theme}, Cmd.none
  | ThemePersisted -> model, Cmd.none
  | TodosMsg todosMsg ->
    let (newTodosModel, newTodosCmd) = TodosPage.update todosMsg model.Todos
    let filterNavigationCmd =
      match todosMsg, model.Page with
      | TodosPage.SetFilter filter, TodosPage userId ->
        if model.Todos.Filter = filter then
          Cmd.none
        else
          Navigation.newUrl (toTodosHashPath userId filter)
      | _ -> Cmd.none
    // Check if this is a logout request from TodosPage
    match todosMsg with
    | TodosPage.RequestLogout ->
      {model with Todos = newTodosModel},
      Cmd.batch [
        Cmd.map TodosMsg newTodosCmd
        filterNavigationCmd
        Cmd.ofMsg RequestLogout
      ]
    | _ ->
      {model with Todos = newTodosModel},
      Cmd.batch [
        Cmd.map TodosMsg newTodosCmd
        filterNavigationCmd
      ]
  | LoginMsg loginMsg ->
    let (newLoginModel, newLoginCmd) = LoginPage.update loginMsg model.Login
    // Check if login was successful
    match loginMsg with
    | LoginPage.LoginResult (Ok user) ->
      {
        model with
            User = Some user
            AuthChecked = true
            Login = newLoginModel
            LogoutError = None
            IsLoggingOut = false
      },
      Cmd.batch [
        Cmd.map LoginMsg newLoginCmd
        Navigation.newUrl (toTodosHashPath user.Id model.Todos.Filter)
      ]
    | _ -> {model with Login = newLoginModel}, Cmd.map LoginMsg newLoginCmd

  | RegisterMsg registerMsg ->
    let (newRegisterModel, newRegisterCmd) =
      RegisterPage.update registerMsg model.Register
    // Check if registration was successful
    match registerMsg with
    | RegisterPage.RegisterResult (Ok user) ->
      {
        model with
            User = Some user
            AuthChecked = true
            Register = newRegisterModel
            LogoutError = None
            IsLoggingOut = false
      },
      Cmd.batch [
        Cmd.map RegisterMsg newRegisterCmd
        Navigation.newUrl (toTodosHashPath user.Id model.Todos.Filter)
      ]
    | _ -> {model with Register = newRegisterModel}, Cmd.map RegisterMsg newRegisterCmd

  | InitAuthResult result ->
    let nextUser =
      match result with
      | Ok user -> Some user
      | Error _ -> None

    let modelWithAuth = {model with User = nextUser; AuthChecked = true}
    let guardedPage = guardPageForUser modelWithAuth.Page modelWithAuth.User
    let shouldRedirect = guardedPage <> modelWithAuth.Page

    let redirectCmd =
      if shouldRedirect then
        Navigation.newUrl (routeForPage guardedPage model.Todos.Filter)
      else
        Cmd.none

    let loadTodosCmd =
      if shouldRedirect then
        Cmd.none
      else
        match guardedPage, modelWithAuth.User with
        | TodosPage _, Some _ -> Cmd.ofMsg (TodosMsg TodosPage.LoadTodos)
        | _ -> Cmd.none

    {modelWithAuth with Page = guardedPage}, Cmd.batch [redirectCmd; loadTodosCmd]

  | LogoutResult (Ok ()) ->
    // On logout success, clear auth-related state and trigger URL navigation.
    // The resulting URL change will run through urlUpdate and set the page.
    let (newTodosModel, _) = TodosPage.init ()
    {
      model with
          User = None
          AuthChecked = true
          IsLoggingOut = false
          LogoutError = None
          Todos = newTodosModel
          Login = LoginPage.init ()
          Register = RegisterPage.init ()
    },
    Navigation.newUrl (toHashPath LoginPage)
  | LogoutResult (Error err) ->
    {model with IsLoggingOut = false; LogoutError = Some (Auth.appErrorToMessage err)}, Cmd.none
  | RequestLogout -> {model with IsLoggingOut = true; LogoutError = None}, Auth.logout LogoutResult // Call logout API

// View
let view (model: Model) (dispatch: Msg -> unit) =
  let onToggleTheme () = ToggleTheme |> dispatch

  Html.div [
    prop.className "relative"
    prop.children [
      match model.Page with
      | TodosPage _ -> TodosPage.view model.Theme model.User model.Todos (TodosMsg >> dispatch) onToggleTheme
      | LoginPage -> LoginPage.view model.Theme model.Login (LoginMsg >> dispatch) onToggleTheme
      | RegisterPage -> RegisterPage.view model.Theme model.Register (RegisterMsg >> dispatch) onToggleTheme
      if model.LogoutError.IsSome then
        Html.div [
          prop.className
            "fixed left-1/2 top-6 z-50 -translate-x-1/2 rounded-md bg-red-500 px-4 py-2 text-sm font-medium text-white shadow-lg"
          prop.text $"Logout failed: {model.LogoutError.Value}"
        ]
      if model.IsLoggingOut then
        Html.div [
          prop.className
            "fixed left-1/2 top-20 z-50 -translate-x-1/2 rounded-md bg-navy-850 px-4 py-2 text-sm font-medium text-white shadow-lg"
          prop.text "Logging out..."
        ]
    ]
  ]

// Program
let start () =
  Program.mkProgram init update view
  |> Program.toNavigable urlParser urlUpdate
  |> Program.withReactBatched "root"
  |> Program.run

do start ()
