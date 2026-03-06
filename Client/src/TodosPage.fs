module TodosPage

open Elmish
open Feliz
open Shared
open ClientShared
open System
open Browser.Dom

// =============== Domain ===============

type Filter =
  | All
  | Active
  | Completed

// =============== State ===============

type TodosState =
  | Loading
  | Loaded of Todo list
  | Errored of string

type Model = {
  Todos: TodosState
  NewTodoText: string
  Filter: Filter
  DraggedTodoId: int option
  DragTargetTodoId: int option
}

// Msg
type Msg =
  // UI
  | NewTodoTextChanged of string
  | AddTodo
  | ToggleTodo of int
  | DeleteTodo of int
  | SetFilter of Filter
  | ClearCompleted
  | RequestLogout
  | LoadTodos
  | DragStarted of int
  | DragEntered of int
  | DragEnded
  | DroppedOnTodo of int
  // API results
  | GotTodos of Result<Todo list, AppError>
  | TodoAdded of Result<Todo, AppError>
  | TodoToggled of Result<Todo, AppError>
  | TodoDeleted of Result<int, AppError>
  | CompletedCleared of Result<unit, AppError>
  | TodoMoved of Result<unit, AppError>

let init () : Model * Cmd<Msg> =
  {
    Todos = Loading
    NewTodoText = ""
    Filter = All
    DraggedTodoId = None
    DragTargetTodoId = None
  },
  Cmd.none // Todos will be loaded by the main program

// =============== API ===============

module Api =
  let getTodos () : Cmd<Msg> =
    Cmd.OfAsync.either
      ApiClient.TodoApi.GetTodos
      ()
      GotTodos
      (asUnexpected GotTodos)

  let addTodo (text: string) : Cmd<Msg> =
    Cmd.OfAsync.either
      ApiClient.TodoApi.CreateTodo
      {Text = text}
      TodoAdded
      (asUnexpected TodoAdded)

  let toggleTodo (id: int) : Cmd<Msg> =
    Cmd.OfAsync.either
      ApiClient.TodoApi.ToggleTodo
      id
      TodoToggled
      (asUnexpected TodoToggled)

  let deleteTodo (id: int) : Cmd<Msg> =
    Cmd.OfAsync.either
      ApiClient.TodoApi.DeleteTodo
      id
      (fun result -> result |> Result.map (fun () -> id) |> TodoDeleted)
      (asUnexpected TodoDeleted)

  let clearCompleted () : Cmd<Msg> =
    Cmd.OfAsync.either
      ApiClient.TodoApi.ClearCompleted
      ()
      CompletedCleared
      (asUnexpected CompletedCleared)

  let moveTodo (todoId: int) (newPosition: int) : Cmd<Msg> =
    Cmd.OfAsync.either
      ApiClient.TodoApi.MoveTodo
      {TodoId = todoId; NewPosition = newPosition}
      TodoMoved
      (asUnexpected TodoMoved)

// =============== Update ===============

/// <summary>
/// Reorder a todo by moving one item to a target index.
/// </summary>
/// <remarks>
/// - Index contract: <c>toIndex</c> is the hovered row index in the original (pre-removal) list.
/// - Geometry of the move:
///   - moving up: destination indices are unchanged.
///   - moving down: removing the source shifts destination rows left by one.
/// - Result: inserting into the post-removal list at the same <c>toIndex</c> lands on the hovered row.
/// </remarks>
/// <param name="todos">Original todo ordering.</param>
/// <param name="fromIndex">Source index in the original list.</param>
/// <param name="toIndex">Hovered destination index in the original list.</param>
let private moveTodoInList (todos: Todo list) (fromIndex: int) (toIndex: int) =
  if fromIndex = toIndex then
    todos
  else
    let moved = List.item fromIndex todos
    todos
    |> List.removeAt fromIndex
    |> List.insertAt toIndex moved

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
  let logApiError action err =
    console.error ($"{action} failed", Auth.appErrorToMessage err)

  match msg with
  | NewTodoTextChanged text -> {model with NewTodoText = text}, Cmd.none
  | AddTodo ->
    let text = model.NewTodoText.Trim ()
    if String.IsNullOrWhiteSpace text then
      model, Cmd.none
    else
      {model with NewTodoText = ""}, Api.addTodo text
  | ToggleTodo id -> model, Api.toggleTodo id
  | DeleteTodo id -> model, Api.deleteTodo id
  | SetFilter f -> {model with Filter = f}, Cmd.none
  | ClearCompleted -> model, Api.clearCompleted ()
  | RequestLogout -> model, Cmd.none // Parent will handle this
  | DragStarted todoId ->
    {
      model with
        DraggedTodoId = Some todoId
        DragTargetTodoId = Some todoId
    },
    Cmd.none
  | DragEntered todoId ->
    match model.DraggedTodoId with
    | Some draggedId when draggedId <> todoId ->
      {model with DragTargetTodoId = Some todoId}, Cmd.none
    | _ -> model, Cmd.none
  | DragEnded ->
    {
      model with
        DraggedTodoId = None
        DragTargetTodoId = None
    },
    Cmd.none
  | DroppedOnTodo targetTodoId ->
    match model.Todos, model.DraggedTodoId with
    | Loaded todos, Some draggedTodoId ->
      let draggedIdx = todos |> List.tryFindIndex (fun t -> t.Id = draggedTodoId)
      let targetIdx = todos |> List.tryFindIndex (fun t -> t.Id = targetTodoId)

      match draggedIdx, targetIdx with
      | Some fromIdx, Some rawToIdx when fromIdx <> rawToIdx ->
        // toIdx is the final index contract used by both UI reorder and API persistence.
        let toIdx = rawToIdx

        let reordered = moveTodoInList todos fromIdx toIdx
        {
          model with
            Todos = Loaded reordered
            DraggedTodoId = None
            DragTargetTodoId = None
        },
        Api.moveTodo draggedTodoId toIdx
      | _ ->
        {
          model with
            DraggedTodoId = None
            DragTargetTodoId = None
        },
        Cmd.none
    | _ ->
      {
        model with
          DraggedTodoId = None
          DragTargetTodoId = None
      },
      Cmd.none
  | LoadTodos -> model, Api.getTodos ()

  | GotTodos (Ok todos) -> {model with Todos = Loaded todos}, Cmd.none
  | GotTodos (Error err) -> {model with Todos = Errored (Auth.appErrorToMessage err)}, Cmd.none

  | TodoAdded (Ok todo) ->
    let next =
      match model.Todos with
      | Loaded xs -> Loaded (xs @ [todo])
      | _ -> Loaded [todo]
    {model with Todos = next}, Cmd.none
  | TodoAdded (Error err) ->
    logApiError "AddTodo" err
    model, Cmd.none

  | TodoToggled (Ok updated) ->
    let next =
      match model.Todos with
      | Loaded xs ->
        Loaded (
          xs
          |> List.map (fun t -> if t.Id = updated.Id then updated else t)
        )
      | other -> other
    {model with Todos = next}, Cmd.none
  | TodoToggled (Error err) ->
    logApiError "ToggleTodo" err
    model, Cmd.none

  | TodoDeleted (Ok id) ->
    let next =
      match model.Todos with
      | Loaded xs -> Loaded (xs |> List.filter (fun t -> t.Id <> id))
      | other -> other
    {model with Todos = next}, Cmd.none
  | TodoDeleted (Error err) ->
    logApiError "DeleteTodo" err
    model, Cmd.none

  | CompletedCleared (Ok ()) -> model, Api.getTodos ()
  | CompletedCleared (Error err) ->
    logApiError "ClearCompleted" err
    model, Cmd.none
  | TodoMoved (Ok ()) -> model, Cmd.none
  | TodoMoved (Error err) ->
    logApiError "MoveTodo" err
    model, Api.getTodos ()

// =============== View ===============

let view
  theme
  (user: User option)
  (model: Model)
  (dispatch: Msg -> unit)
  (onToggleTheme: unit -> unit)
  =
  let isDark = string theme = "Dark"
  let filterButton (label: string, filter: Filter) =
    Html.button [
      prop.className ("cursor-pointer font-bold " + if model.Filter = filter then "text-blue-500" else if isDark then "text-navy-850 hover:text-purple-100" else "text-gray-600 hover:text-navy-850")
      prop.onClick (fun _ -> SetFilter filter |> dispatch)
      prop.text label
    ]

  let listAndFooter =
    match model.Todos with
    | Loading ->
      Html.div [
        prop.className "p-5 text-center text-gray-600 dark:text-purple-700"
        prop.text "Loading..."
      ]
    | Errored msg ->
      Html.div [
        prop.className "p-5 text-center text-red-500"
        prop.text $"Failed to load todos: {msg}"
      ]
    | Loaded todos ->
      let filtered =
        match model.Filter with
        | All -> todos
        | Active -> todos |> List.filter (fun t -> not t.Completed)
        | Completed -> todos |> List.filter (fun t -> t.Completed)

      let todoIndexById =
        todos
        |> List.mapi (fun idx todo -> todo.Id, idx)
        |> Map.ofList

      let tryIndexById todoId = Map.tryFind todoId todoIndexById

      let isMovingDownTo targetTodoId =
        match model.DraggedTodoId |> Option.bind tryIndexById, tryIndexById targetTodoId with
        | Some fromIdx, Some toIdx -> fromIdx < toIdx
        | _ -> false

      let dropIndicatorClass isDropTarget isDragged targetTodoId =
        if not isDropTarget || isDragged then
          ""
        else
          let directionClass = if isMovingDownTo targetTodoId then "border-b-2" else "border-t-2"
          let colorClass = if isDark then "border-blue-400" else "border-blue-500"
          directionClass + " " + colorClass

      let itemsLeft =
        todos
        |> List.filter (fun t -> not t.Completed)
        |> List.length

      Html.div [
        prop.className ("rounded-md shadow-xl transition-colors duration-300 divide-y " + if isDark then "bg-navy-900 divide-purple-800" else "bg-white divide-gray-300")
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
                  let isDragged =
                    model.DraggedTodoId
                    |> Option.exists (fun draggedId -> draggedId = todo.Id)

                  let isDropTarget =
                    model.DragTargetTodoId
                    |> Option.exists (fun targetId -> targetId = todo.Id)

                  let indicatorClass = dropIndicatorClass isDropTarget isDragged todo.Id

                  Html.li [
                    prop.key todo.Id
                    prop.className (
                      String.concat " " [
                        "group flex items-center gap-4 px-5 py-4 cursor-pointer active:cursor-grabbing"
                        indicatorClass
                        if isDragged then
                          "opacity-60"
                      ]
                    )
                    prop.draggable true
                    prop.onDragStart (fun ev ->
                      ev.dataTransfer.setData ("text/plain", string todo.Id) |> ignore
                      ev.dataTransfer.effectAllowed <- "move"
                      DragStarted todo.Id |> dispatch
                    )
                    prop.onDragEnter (fun _ -> DragEntered todo.Id |> dispatch)
                    prop.onDragOver (fun ev -> ev.preventDefault ())
                    prop.onDrop (fun ev ->
                      ev.preventDefault ()
                      DroppedOnTodo todo.Id |> dispatch
                    )
                    prop.onDragEnd (fun _ -> DragEnded |> dispatch)
                    prop.children [
                      Html.button [
                        prop.className ("cursor-pointer w-6 h-6 rounded-full flex items-center justify-center " + if todo.Completed then "bg-gradient-to-br from-gradient-1-left to-gradient-1-right" else if isDark then "border border-purple-800" else "border border-gray-300")
                        prop.onClick (fun _ -> ToggleTodo todo.Id |> dispatch)
                        prop.children [
                          if todo.Completed then
                            Html.img [
                              prop.src "/images/icon-check.svg"
                              prop.alt "Checked"
                            ]
                        ]
                      ]
                      Html.p [
                        prop.className ("grow " + if todo.Completed then (if isDark then "line-through text-purple-700" else "line-through text-gray-300") else if isDark then "text-purple-300" else "text-navy-850")
                        prop.text todo.Text
                      ]
                      Html.button [
                        prop.className "cursor-pointer opacity-0 group-hover:opacity-100 transition-opacity"
                        prop.onClick (fun _ -> DeleteTodo todo.Id |> dispatch)
                        prop.children [
                          Html.img [
                            prop.src "/images/icon-cross.svg"
                            prop.alt "Delete todo"
                          ]
                        ]
                      ]
                    ]
                  ]
            ]
          ]
          Html.div [
            prop.className ("flex justify-between items-center text-sm p-4 " + if isDark then "text-purple-700" else "text-gray-600")
            prop.children [
              Html.p [prop.text $"{itemsLeft} items left"]
              Html.div [
                prop.className "hidden md:flex gap-4"
                prop.children [
                  filterButton ("All", All)
                  filterButton ("Active", Active)
                  filterButton ("Completed", Completed)
                ]
              ]
              Html.button [
                prop.className ("cursor-pointer " + if isDark then "hover:text-purple-100" else "hover:text-navy-850")
                prop.onClick (fun _ -> ClearCompleted |> dispatch)
                prop.text "Clear Completed"
              ]
            ]
          ]
        ]
      ]

  Html.div [
    prop.className ("min-h-screen transition-colors duration-300 " + if isDark then "bg-navy-950" else "bg-gray-50")
    prop.style [
      style.fontFamily "var(--font-josefin-sans)"
      style.fontSize 18
    ]
    prop.children [
      Html.div [
        prop.className ("h-[200px] md:h-[300px] bg-no-repeat bg-cover " + if isDark then "bg-[url('/images/bg-mobile-dark.jpg')] md:bg-[url('/images/bg-desktop-dark.jpg')]" else "bg-[url('/images/bg-mobile-light.jpg')] md:bg-[url('/images/bg-desktop-light.jpg')]")
      ]
      Html.main [
        prop.className "relative px-6 md:px-0 md:max-w-xl mx-auto -mt-36 md:-mt-48"
        prop.children [
          Html.div [
            prop.className "flex justify-between items-center mb-8"
            prop.children [
              Html.h1 [
                prop.className "text-3xl md:text-4xl font-bold text-white tracking-[0.3em]"
                prop.text "TODO"
              ]
              Html.div [
                prop.className "flex items-center gap-4"
                prop.children [
                  Html.button [
                    prop.className "cursor-pointer text-white hover:text-gray-300 text-sm font-medium"
                    prop.onClick (fun _ -> RequestLogout |> dispatch)
                    prop.text "Logout"
                  ]
                  Html.button [
                    prop.className "cursor-pointer"
                    prop.onClick (fun _ -> onToggleTheme ())
                    prop.children [
                      Html.img [
                        prop.src (if isDark then "/images/icon-sun.svg" else "/images/icon-moon.svg")
                        prop.alt "Toggle theme"
                      ]
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
                prop.className ("flex items-center gap-4 px-5 py-3.5 rounded-md transition-colors duration-300 " + if isDark then "bg-navy-900" else "bg-white")
                prop.onSubmit (fun ev ->
                  ev.preventDefault ()
                  AddTodo |> dispatch
                )
                prop.children [
                  Html.button [
                    prop.type' "submit"
                    prop.className ("cursor-pointer appearance-none p-0 w-6 h-6 aspect-square rounded-full flex items-center justify-center flex-shrink-0 border " + if isDark then "border-purple-800" else "border-gray-300")
                    prop.style [
                      style.minWidth 24
                      style.minHeight 24
                    ]
                  ]
                  Html.input [
                    prop.className ("cursor-text w-full bg-transparent border-none outline-none focus:outline-none focus:ring-0 focus:border-none " + if isDark then "text-purple-300 placeholder:text-purple-700" else "text-navy-850 placeholder:text-gray-600")
                    prop.value model.NewTodoText
                    prop.placeholder "Create a new todo..."
                    prop.onChange (NewTodoTextChanged >> dispatch)
                  ]
                ]
              ]
            ]
          ]
          listAndFooter
          Html.div [
            prop.className ("md:hidden mt-4 p-4 rounded-md flex justify-center gap-4 shadow-xl transition-colors duration-300 " + if isDark then "bg-navy-900" else "bg-white")
            prop.children [
              filterButton ("All", All)
              filterButton ("Active", Active)
              filterButton ("Completed", Completed)
            ]
          ]
          Html.p [
            prop.className ("text-center text-sm mt-10 " + if isDark then "text-purple-700" else "text-gray-600")
            prop.text "Drag and drop to reorder list"
          ]
          if user.IsSome then
            Html.p [
              prop.className ("text-center text-sm mt-4 " + if isDark then "text-purple-700" else "text-gray-600")
              prop.text $"Logged in as {user.Value.Email}"
            ]
        ]
      ]
    ]
  ]
