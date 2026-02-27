module Todos

open Data
open FsToolkit.ErrorHandling
open Giraffe
open System.Threading.Tasks
open Shared

let private toSharedTodo (todo: Entity.Todo) : Todo = {
    Id = todo.Id
    Text = todo.Text
    Completed = todo.Completed
}

let private requireUserId ctx =
    Auth.tryGetUserId ctx |> AsyncResult.ofResult

let private fromTask (work: Task<'a>) =
    work |> Async.AwaitTask |> AsyncResult.ofAsync

let private getTodos ctx db () =
    asyncResult {
        let! userId = requireUserId ctx
        let! todos = getAllTodos db userId |> fromTask
        return todos |> Seq.map toSharedTodo |> List.ofSeq
    }

let private createTodo ctx db payload =
    asyncResult {
        let! userId = requireUserId ctx

        if System.String.IsNullOrWhiteSpace payload.Text then
            return! Error(ValidationError "Todo text is required.")

        let! created = insertTodo db userId payload.Text |> fromTask
        return toSharedTodo created
    }

let private toggleTodoById ctx db id =
    asyncResult {
        let! userId = requireUserId ctx
        let! updated = toggleTodo db userId id |> fromTask

        match updated with
        | Some todo -> return toSharedTodo todo
        | None -> return! Error NotFound
    }

let private deleteTodoById ctx db id =
    asyncResult {
        let! userId = requireUserId ctx
        let! deleted = deleteTodo db userId id |> fromTask

        if not deleted then
            return! Error NotFound

        return ()
    }

let private clearCompletedTodos ctx db () =
    asyncResult {
        let! userId = requireUserId ctx
        do! clearCompleted db userId |> fromTask
        return ()
    }

let private moveTodoByPosition ctx db payload =
    asyncResult {
        let! userId = requireUserId ctx

        if payload.NewPosition < 0 then
            return! Error(ValidationError "Todo position must be non-negative.")

        let! moved = moveTodo db userId payload.TodoId payload.NewPosition |> fromTask

        if not moved then
            return! Error NotFound

        return ()
    }

let todoApiImplementation (ctx: Microsoft.AspNetCore.Http.HttpContext) : ITodoApi =
    let db = ctx.GetService<Entity.AppDbContext>()

    {
        GetTodos = getTodos ctx db
        CreateTodo = createTodo ctx db
        ToggleTodo = toggleTodoById ctx db
        DeleteTodo = deleteTodoById ctx db
        ClearCompleted = clearCompletedTodos ctx db
        MoveTodo = moveTodoByPosition ctx db
    }

let todosApiHandler: HttpHandler =
    RemotingUtil.handlerFromApi todoApiImplementation
