module TodosPage

open Elmish
open Feliz
open Json
open Shared
open Fable.Core
open Fable.Core.JsInterop
open Fable.SimpleHttp
open System
open Browser.Dom

// =============== Domain ===============

type Filter =
  | All
  | Active
  | Completed

type Todo = 
  { Id: int
    Text: string
    Completed: bool }

type NewTodo = { Text: string }

// =============== State ===============

type TodosState =
  | Loading
  | Loaded of Todo list
  | Errored of string

type Model =
  { Todos: TodosState
    NewTodoText: string
    Filter: Filter
    Theme: Theme } 

// Msg
type Msg =
  // UI
  | NewTodoTextChanged of string
  | AddTodo
  | ToggleTodo of int
  | DeleteTodo of int
  | SetFilter of Filter
  | ClearCompleted
  | ToggleTheme
  | RequestLogout
  | LoadTodos
  // API results
  | GotTodos of Result<Todo list, exn>
  | TodoAdded of Result<Todo, exn>
  | TodoToggled of Result<Todo, exn>
  | TodoDeleted of Result<int, exn>
  | CompletedCleared of Result<unit, exn>

let init (theme: Theme) : Model * Cmd<Msg> =
  { Todos = Loading
    NewTodoText = ""
    Filter = All
    Theme = theme },
  Cmd.none // Todos will be loaded by the main program

// =============== JSON (defensive) ===============

let parseTodoObj (o: obj) : Todo =
  let id =
    getFirst<int> o ["id"; "Id"] |> Option.defaultValue 0
  let text =
    getFirst<string> o ["text"; "Text"] |> Option.defaultValue ""
  let completed =
    match getFirst<bool> o ["completed"; "Completed"] with
    | Some b -> b
    | None ->
        match getFirst<float> o ["completed"; "Completed"] with
        | Some n -> n <> 0.0
        | None -> false
  { Id = id; Text = text; Completed = completed }

let parseTodosJson (json: string) : Result<Todo list, exn> =
  try
    let parsed = JS.JSON.parse(json)
    if JS.Constructors.Array.isArray(parsed) then
      let items : obj array = unbox parsed
      items |> Array.map parseTodoObj |> Array.toList |> Ok
    else
      let inner = (parsed?todos: obj)
      if not (isNull inner) && not (isUndef inner) && JS.Constructors.Array.isArray(inner) then
        let items : obj array = unbox inner
        items |> Array.map parseTodoObj |> Array.toList |> Ok
      else
        Error (exn "Unexpected todos JSON shape")
  with ex -> Error ex

let parseTodoJson (json: string) : Result<Todo, exn> =
  try Ok (JS.JSON.parse(json) |> parseTodoObj)
  with ex -> Error ex

let encodeNewTodo (t: NewTodo) =
  JS.JSON.stringify(createObj [ "text" ==> t.Text ])

// =============== API ===============

module Api =
  let private is2xx (status:int) = status >= 200 && status < 300
  let private bodyOrEmpty (s: string) = if isNull s then "" else s

  let getTodos () : Cmd<Msg> =
    let fetch () = async {
      let! res =
        Http.request "/api/todos"
        |> Http.method GET
        |> Http.send
      let status = res.statusCode
      let body = bodyOrEmpty res.responseText
      if status = 401 then
        return Error (exn "Unauthorized")
      elif not (is2xx status) then
        return Error (exn (sprintf "HTTP %d: %s" status body))
      else
        return parseTodosJson body
    }
    Cmd.OfAsync.either fetch () GotTodos (fun ex -> GotTodos (Error ex))

  let addTodo (text:string) : Cmd<Msg> =
    let fetch () = async {
      let payload = encodeNewTodo { Text = text }
      let! res =
        Http.request "/api/todos"
        |> Http.method POST
        |> Http.header (Headers.contentType "application/json")
        |> Http.content (BodyContent.Text payload)
        |> Http.send
      let status = res.statusCode
      let body = bodyOrEmpty res.responseText
      if status = 401 then
        return Error (exn "Unauthorized")
      elif not (is2xx status) then
        return Error (exn (sprintf "HTTP %d: %s" status body))
      else
        return parseTodoJson body
    }
    Cmd.OfAsync.either fetch () TodoAdded (fun ex -> TodoAdded (Error ex))

  let toggleTodo (id:int) : Cmd<Msg> =
    let fetch () = async {
      let! res =
        Http.request (sprintf "/api/todos/%d/toggle" id)
        |> Http.method PUT
        |> Http.header (Headers.contentType "application/json")
        |> Http.content (BodyContent.Text "{}")
        |> Http.send
      let status = res.statusCode
      let body = bodyOrEmpty res.responseText
      if status = 401 then
        return Error (exn "Unauthorized")
      elif not (is2xx status) then
        return Error (exn (sprintf "HTTP %d: %s" status body))
      else
        return parseTodoJson body
    }
    Cmd.OfAsync.either fetch () TodoToggled (fun ex -> TodoToggled (Error ex))

  let deleteTodo (id:int) : Cmd<Msg> =
    let fetch () = async {
      let! res =
        Http.request (sprintf "/api/todos/%d" id)
        |> Http.method DELETE
        |> Http.send
      let status = res.statusCode
      let body = bodyOrEmpty res.responseText
      if status = 401 then
        return Error (exn "Unauthorized")
      elif status = 404 then
        return Ok id
      elif not (is2xx status) then
        return Error (exn (sprintf "HTTP %d: %s" status body))
      else
        return Ok id
    }
    Cmd.OfAsync.either fetch () TodoDeleted (fun ex -> TodoDeleted (Error ex))

  let clearCompleted () : Cmd<Msg> =
    let fetch () = async {
      let! res =
        Http.request "/api/todos/completed"
        |> Http.method DELETE
        |> Http.send
      let status = res.statusCode
      let body = bodyOrEmpty res.responseText
      if status = 401 then
        return Error (exn "Unauthorized")
      elif not (is2xx status) then
        return Error (exn (sprintf "HTTP %d: %s" status body))
      else
        return Ok ()
    }
    Cmd.OfAsync.either fetch () CompletedCleared (fun ex -> CompletedCleared (Error ex))

// =============== Update ===============

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
  match msg with
  | NewTodoTextChanged text -> { model with NewTodoText = text }, Cmd.none
  | AddTodo ->
      let text = model.NewTodoText.Trim()
      if String.IsNullOrWhiteSpace text then model, Cmd.none
      else { model with NewTodoText = "" }, Api.addTodo text
  | ToggleTodo id -> model, Api.toggleTodo id
  | DeleteTodo id -> model, Api.deleteTodo id
  | SetFilter f -> { model with Filter = f }, Cmd.none
  | ClearCompleted -> model, Api.clearCompleted ()
  | RequestLogout -> model, Cmd.none // Parent will handle this
  | ToggleTheme -> { model with Theme = (match model.Theme with Light -> Dark | Dark -> Light) }, Cmd.none
  | LoadTodos -> model, Api.getTodos ()

  | GotTodos (Ok todos) ->
      { model with Todos = Loaded todos }, Cmd.none
  | GotTodos (Error e) ->
      { model with Todos = Errored e.Message }, Cmd.none

  | TodoAdded (Ok todo) ->
      let next =
        match model.Todos with
        | Loaded xs -> Loaded (xs @ [todo])
        | _ -> Loaded [todo]
      { model with Todos = next }, Cmd.none
  | TodoAdded (Error e) ->
      console.error("AddTodo failed", e); model, Cmd.none

  | TodoToggled (Ok updated) ->
      let next =
        match model.Todos with
        | Loaded xs -> Loaded (xs |> List.map (fun t -> if t.Id = updated.Id then updated else t))
        | other -> other
      { model with Todos = next }, Cmd.none
  | TodoToggled (Error e) ->
      console.error("ToggleTodo failed", e); model, Cmd.none

  | TodoDeleted (Ok id) ->
      let next =
        match model.Todos with
        | Loaded xs -> Loaded (xs |> List.filter (fun t -> t.Id <> id))
        | other -> other
      { model with Todos = next }, Cmd.none
  | TodoDeleted (Error e) ->
      console.error("DeleteTodo failed", e); model, Cmd.none

  | CompletedCleared (Ok ()) ->
      model, Api.getTodos ()
  | CompletedCleared (Error e) ->
      console.error("ClearCompleted failed", e); model, Cmd.none

// =============== View ===============

let view (user: User option) (model: Model) (dispatch: Msg -> unit) =
  let isDark = model.Theme = Dark

  let filterButton (label: string, filter: Filter) =
    Html.button [ 
      prop.className (
        if model.Filter = filter then "font-bold text-blue-500"
        else if isDark then "font-bold text-navy-850 hover:text-purple-100"
        else "font-bold text-gray-600 hover:text-navy-850"
      )
      prop.onClick (fun _ -> dispatch (SetFilter filter))
      prop.text label
    ]

  let listAndFooter =
    match model.Todos with
    | Loading ->
        Html.div [ prop.className "p-5 text-center text-gray-600 dark:text-purple-700"; prop.text "Loading..." ]
    | Errored msg ->
        Html.div [ prop.className "p-5 text-center text-red-500"; prop.text $"Failed to load todos: {msg}" ]
    | Loaded todos ->
        let filtered =
          match model.Filter with
          | All -> todos
          | Active -> todos |> List.filter (fun t -> not t.Completed)
          | Completed -> todos |> List.filter (fun t -> t.Completed)
        let itemsLeft = todos |> List.filter (fun t -> not t.Completed) |> List.length

        Html.div [ 
          prop.className (
            if isDark then "rounded-md shadow-xl transition-colors duration-300 divide-y bg-navy-900 divide-purple-800"
            else "rounded-md shadow-xl transition-colors duration-300 divide-y bg-white divide-gray-300"
          )
          prop.children [ 
            Html.ul [ 
              prop.children [ 
                if List.isEmpty filtered then
                  Html.li [ 
                    prop.className "p-4 text-center text-gray-600 dark:text-purple-700"
                    prop.text "No todos here!"
                  ]
                else
                  for todo in filtered do
                    Html.li [ 
                      prop.key todo.Id
                      prop.className "group flex items-center gap-4 px-5 py-4"
                      prop.children [ 
                        Html.button [ 
                          prop.className (
                            if todo.Completed then "w-6 h-6 rounded-full flex items-center justify-center bg-gradient-to-br from-gradient-1-left to-gradient-1-right"
                            else if isDark then "w-6 h-6 rounded-full flex items-center justify-center border border-purple-800"
                            else "w-6 h-6 rounded-full flex items-center justify-center border border-gray-300"
                          )
                          prop.onClick (fun _ -> dispatch (ToggleTodo todo.Id))
                          prop.children [ 
                            if todo.Completed then
                              Html.img [ prop.src "/images/icon-check.svg"; prop.alt "Checked" ] else Html.none
                          ]
                        ]
                        Html.p [ 
                          prop.className (
                            if todo.Completed then
                              if isDark then "grow line-through text-purple-700" else "grow line-through text-gray-300"
                            else if isDark then "grow text-purple-300" else "grow text-navy-850"
                          )
                          prop.text todo.Text
                        ]
                        Html.button [ 
                          prop.className "opacity-0 group-hover:opacity-100 transition-opacity"
                          prop.onClick (fun _ -> dispatch (DeleteTodo todo.Id))
                          prop.children [ Html.img [ prop.src "/images/icon-cross.svg"; prop.alt "Delete todo" ] ]
                        ]
                      ]
                    ]
              ]
            ]
            Html.div [ 
              prop.className (if isDark then "flex justify-between items-center text-sm p-4 text-purple-700"
                              else "flex justify-between items-center text-sm p-4 text-gray-600")
              prop.children [ 
                Html.p [ prop.text $"{itemsLeft} items left" ]
                Html.div [ 
                  prop.className "hidden md:flex gap-4"
                  prop.children [ 
                    filterButton ("All", All)
                    filterButton ("Active", Active)
                    filterButton ("Completed", Completed)
                  ]
                ]
                Html.button [ 
                  prop.className (if isDark then "hover:text-purple-100" else "hover:text-navy-850")
                  prop.onClick (fun _ -> dispatch ClearCompleted)
                  prop.text "Clear Completed"
                ]
              ]
            ]
          ]
        ]

  Html.div [ 
    prop.className (if isDark then "min-h-screen transition-colors duration-300 bg-navy-950"
                    else "min-h-screen transition-colors duration-300 bg-gray-50")
    prop.style [ style.fontFamily "var(--font-josefin-sans)"; style.fontSize 18 ]
    prop.children [ 
      Html.div [ 
        prop.className (
          if isDark then
            "h-[200px] md:h-[300px] bg-no-repeat bg-cover bg-[url('/images/bg-mobile-dark.jpg')] md:bg-[url('/images/bg-desktop-dark.jpg')]"
          else
            "h-[200px] md:h-[300px] bg-no-repeat bg-cover bg-[url('/images/bg-mobile-light.jpg')] md:bg-[url('/images/bg-desktop-light.jpg')]"
        )
      ]
      Html.main [ 
        prop.className "relative px-6 md:px-0 md:max-w-xl mx-auto -mt-36 md:-mt-48"
        prop.children [ 
          Html.div [ 
            prop.className "flex justify-between items-center mb-8"
            prop.children [ 
              Html.h1 [ prop.className "text-3xl md:text-4xl font-bold text-white tracking-[0.3em]"; prop.text "TODO" ]
              Html.div [ 
                prop.className "flex items-center gap-4"
                prop.children [
                  Html.button [ 
                    prop.className "text-white hover:text-gray-300 text-sm font-medium"
                    prop.onClick (fun _ -> dispatch RequestLogout)
                    prop.text "Logout"
                  ]
                  Html.button [ 
                    prop.onClick (fun _ -> dispatch ToggleTheme)
                    prop.children [ 
                      Html.img [ prop.src (if isDark then "/images/icon-sun.svg" else "/images/icon-moon.svg"); prop.alt "Toggle theme" ]
                    ]
                  ]
                ]
              ]
            ]
          ]
          Html.div [ 
            prop.className "mb-6"
            prop.children [ 
              Html.form [ 
                prop.className (
                  if isDark then "flex items-center gap-4 px-5 py-3.5 rounded-md transition-colors duration-300 bg-navy-900"
                  else "flex items-center gap-4 px-5 py-3.5 rounded-md transition-colors duration-300 bg-white"
                )
                prop.onSubmit (fun ev -> ev.preventDefault(); dispatch AddTodo)
                prop.children [ 
                  Html.button [ 
                    prop.type' "submit"
                    prop.className (
                      if isDark then "w-6 h-6 border rounded-full flex-shrink-0 border-purple-800"
                      else "w-6 h-6 border rounded-full flex-shrink-0 border-gray-300"
                    )
                  ]
                  Html.input [ 
                    prop.className (
                      if isDark then "w-full bg-transparent outline-none text-purple-300 placeholder:text-purple-700"
                      else "w-full bg-transparent outline-none text-navy-850 placeholder:text-gray-600"
                    )
                    prop.value model.NewTodoText
                    prop.placeholder "Create a new todo..."
                    prop.onChange (fun v -> dispatch (NewTodoTextChanged v))
                  ]
                ]
              ]
            ]
          ]
          listAndFooter
          Html.div [ 
            prop.className (
              if isDark then "md:hidden mt-4 p-4 rounded-md flex justify-center gap-4 shadow-xl transition-colors duration-300 bg-navy-900"
              else "md:hidden mt-4 p-4 rounded-md flex justify-center gap-4 shadow-xl transition-colors duration-300 bg-white"
            )
            prop.children [ 
              filterButton ("All", All)
              filterButton ("Active", Active)
              filterButton ("Completed", Completed)
            ]
          ]
          (match user with
           | Some u ->
                Html.p [
                    prop.className (if isDark then "text-center text-sm mt-4 text-purple-700" else "text-center text-sm mt-4 text-gray-600")
                    prop.text $"Logged in as {u.Email}"
                ]
           | None -> Html.none)
          Html.p [ 
            prop.className (if isDark then "text-center text-sm mt-10 text-purple-700" else "text-center text-sm mt-10 text-gray-600")
            prop.text "Drag and drop to reorder list"
          ]
        ]
      ]
    ]
  ]
