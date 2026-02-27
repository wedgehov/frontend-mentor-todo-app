namespace Shared

type AppError =
    | Unauthorized
    | NotFound
    | Conflict
    | InvalidCredentials
    | ValidationError of string
    | Unexpected of string

type LoginRequest = {
    Email: string
    Password: string
}

type RegisterRequest = {
    Email: string
    Password: string
}

type IAuthApi = {
    Register: RegisterRequest -> Async<Result<User, AppError>>
    Login: LoginRequest -> Async<Result<User, AppError>>
    Logout: unit -> Async<Result<unit, AppError>>
}

type ITodoApi = {
    GetTodos: unit -> Async<Result<Todo list, AppError>>
    CreateTodo: NewTodo -> Async<Result<Todo, AppError>>
    ToggleTodo: int -> Async<Result<Todo, AppError>>
    DeleteTodo: int -> Async<Result<unit, AppError>>
    ClearCompleted: unit -> Async<Result<unit, AppError>>
    MoveTodo: MoveTodoRequest -> Async<Result<unit, AppError>>
}
