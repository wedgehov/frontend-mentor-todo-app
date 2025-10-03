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

// Your urlUpdate must be: Option<Page> -> Model -> Model * Cmd<Msg>
let urlUpdate (result: Page option) (model: Model) : Model * Cmd<Msg> =
    match result with
    | Some page -> { model with Page = page }, Cmd.none
    | None -> model, Cmd.none // or navigate to a default

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
    newModel, Cmd.batch [ todosCmd |> Cmd.map TodosMsg; routeCmd ]

// Update
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | TodosMsg todosMsg ->
        let (newTodosModel, newTodosCmd) = TodosPage.update todosMsg model.Todos
        { model with Todos = newTodosModel }, Cmd.map TodosMsg newTodosCmd
    | LoginMsg loginMsg ->
        let (newLoginModel, newLoginCmd) = LoginPage.update loginMsg model.Login
        // Check if login was successful
        match loginMsg with
        | LoginPage.LoginResult (Ok user) ->
            let (newModel, newCmd) = urlUpdate (Some TodosPage) model
            { newModel with User = Some user }, Cmd.batch [ newCmd; Cmd.map LoginMsg newLoginCmd ]
        | _ -> { model with Login = newLoginModel }, Cmd.map LoginMsg newLoginCmd

    | RegisterMsg registerMsg ->
        let (newRegisterModel, newRegisterCmd) = RegisterPage.update registerMsg model.Register
        // Check if registration was successful
        match registerMsg with
        | RegisterPage.RegisterResult (Ok user) ->
            let (newModel, newCmd) = urlUpdate (Some TodosPage) model
            { newModel with User = Some user }, Cmd.batch [ newCmd; Cmd.map RegisterMsg newRegisterCmd ]
        | _ -> { model with Register = newRegisterModel }, Cmd.map RegisterMsg newRegisterCmd

    | LoggedIn user -> { model with User = Some user }, Cmd.none // Might navigate away
    | LoggedOut -> { model with User = None }, Cmd.none // Might navigate to login

// View
let view (model: Model) (dispatch: Msg -> unit) =
    match model.Page with
    | TodosPage -> TodosPage.view model.Todos (TodosMsg >> dispatch)
    | LoginPage -> LoginPage.view model.Login (LoginMsg >> dispatch)
    | RegisterPage -> RegisterPage.view model.Register (RegisterMsg >> dispatch)

// Program
let start () =
    Program.mkProgram init update view
    |> Program.toNavigable urlParser urlUpdate
    |> Program.withReactBatched "root"
    |> Program.run

do start ()