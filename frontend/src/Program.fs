module App

open Elmish
open Elmish.React
open Elmish.Navigation
open Elmish.UrlParser
open Feliz
open Shared

// Main model
type Page =
    | TodosPage
    | LoginPage
    | RegisterPage

type Model =
    { Page: Page
      User: User option
      Theme: Theme
      Todos: TodosPage.Model
      Login: LoginPage.Model
      Register: RegisterPage.Model }

// Main messages
type Msg =
    | TodosMsg of TodosPage.Msg
    | LoginMsg of LoginPage.Msg
    | RegisterMsg of RegisterPage.Msg
    | LoggedIn of User
    | LoggedOut
    | RequestLogout
    | LoadTodos

// URL parsing and routing
module UP = Elmish.UrlParser

let pageParser =
    UP.oneOf [
        UP.map TodosPage (UP.s "todos")
        UP.map LoginPage (UP.s "login")
        UP.map RegisterPage (UP.s "register")
        UP.map TodosPage UP.top
    ]

let urlParser: Browser.Types.Location -> Page option = UP.parseHash pageParser

let urlUpdate (result: Page option) (model: Model) : Model * Cmd<Msg> =
    match result with
    | Some requestedPage ->
        let needsAuth = match requestedPage with | TodosPage -> true | _ -> false
        let isAuthenticated = model.User.IsSome

        // If user needs auth for the requested page but isn't authenticated,
        // force the page to be LoginPage. Otherwise, use the requested page.
        let actualPage =
            if needsAuth && not isAuthenticated then LoginPage
            else requestedPage

        // When navigating to a page, reset its specific state to clear old form data.
        let newModel =
            match actualPage with
            | LoginPage -> { model with Login = LoginPage.init() }
            | RegisterPage -> { model with Register = RegisterPage.init() }
            | TodosPage -> model // No state to reset on the todos page for now

        { newModel with Page = actualPage }, Cmd.none

    | None -> model, Cmd.none // No page found, do nothing

// Init
let init (result: Option<Page>) : Model * Cmd<Msg> =
    let page = result |> Option.defaultValue TodosPage
    let (todosModel, todosCmd) = TodosPage.init Dark
    let model =
        { Page = page
          User = None
          Theme = Dark
          Todos = todosModel
          Login = LoginPage.init ()
          Register = RegisterPage.init () }
    let (newModel, routeCmd) = urlUpdate (Some page) model
    
    // Check if user is already authenticated and load todos if so
    let authCheckCmd = 
        if newModel.User.IsSome then
            Cmd.ofMsg LoadTodos
        else
            Cmd.none
    
    newModel, Cmd.batch [ todosCmd |> Cmd.map TodosMsg; routeCmd; authCheckCmd ]

// Update
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | TodosMsg todosMsg ->
        let (newTodosModel, newTodosCmd) = TodosPage.update todosMsg model.Todos
        // Check if this is a logout request from TodosPage
        match todosMsg with
        | TodosPage.RequestLogout -> 
            { model with Todos = newTodosModel }, 
            Cmd.batch [ 
                Cmd.map TodosMsg newTodosCmd
                Cmd.ofMsg RequestLogout
            ]
        | _ -> { model with Todos = newTodosModel }, Cmd.map TodosMsg newTodosCmd
    | LoginMsg loginMsg ->
        let (newLoginModel, newLoginCmd) = LoginPage.update loginMsg model.Login
        // Check if login was successful
        match loginMsg with
        | LoginPage.LoginResult (Ok user) ->
            let (newModel, newCmd) = urlUpdate (Some TodosPage) { model with User = Some user; Login = newLoginModel }
            newModel, Cmd.batch [ newCmd; Cmd.map LoginMsg newLoginCmd; Cmd.ofMsg LoadTodos ]
        | _ -> { model with Login = newLoginModel }, Cmd.map LoginMsg newLoginCmd

    | RegisterMsg registerMsg ->
        let (newRegisterModel, newRegisterCmd) = RegisterPage.update registerMsg model.Register
        // Check if registration was successful
        match registerMsg with
        | RegisterPage.RegisterResult (Ok user) ->
            let (newModel, newCmd) = urlUpdate (Some TodosPage) { model with User = Some user; Register = newRegisterModel }
            newModel, Cmd.batch [ newCmd; Cmd.map RegisterMsg newRegisterCmd; Cmd.ofMsg LoadTodos ]
        | _ -> { model with Register = newRegisterModel }, Cmd.map RegisterMsg newRegisterCmd

    | LoggedIn user -> 
        { model with User = Some user }, 
        Cmd.batch [
            Cmd.ofMsg LoadTodos // Load todos when user logs in
        ]
    | LoggedOut ->
        // When logged out, clear the user, re-init todos and navigate to the login page
        let (newTodosModel, _) = TodosPage.init model.Theme
        let (newModel, navCmd) = urlUpdate (Some LoginPage) { model with User = None; Todos = newTodosModel }
        newModel, navCmd
    | RequestLogout -> 
        model, Auth.logout (fun _ -> LoggedOut) // Call logout API
    | LoadTodos ->
        // Load todos by dispatching the LoadTodos command to TodosPage
        let (newTodosModel, newTodosCmd) = TodosPage.update TodosPage.LoadTodos model.Todos
        { model with Todos = newTodosModel }, Cmd.map TodosMsg newTodosCmd

// View
let view (model: Model) (dispatch: Msg -> unit) =
    match model.Page with
    | TodosPage -> TodosPage.view model.User model.Todos (TodosMsg >> dispatch)
    | LoginPage -> LoginPage.view model.Login (LoginMsg >> dispatch)
    | RegisterPage -> RegisterPage.view model.Register (RegisterMsg >> dispatch)

// Program
let start () =
    Program.mkProgram init update view
    |> Program.toNavigable urlParser urlUpdate
    |> Program.withReactBatched "root"
    |> Program.run

do start ()