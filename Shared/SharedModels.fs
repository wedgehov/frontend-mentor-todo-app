namespace Shared

type User = {
    Id: int
    Email: string
}

type Todo = {
    Id: int
    Text: string
    Completed: bool
}

type NewTodo = { Text: string }

type MoveTodoRequest = {
    TodoId: int
    NewPosition: int
}
