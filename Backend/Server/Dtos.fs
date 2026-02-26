module Dtos

[<CLIMutable>]
type NewTodo = { text: string }

[<CLIMutable>]
type UpdateTodo = { text: string option; completed: bool option }

[<CLIMutable>]
type RegisterUserRequest = { email: string; password: string }

[<CLIMutable>]
type LoginUserRequest = { email: string; password: string }
