module Todos

open FsToolkit.ErrorHandling
open Giraffe
open Microsoft.EntityFrameworkCore
open System.Linq
open System.Threading.Tasks
open Shared

let private toSharedTodo (todo: Entity.Todo) : Todo = {
    Id = todo.Id
    Text = todo.Text
    Completed = todo.Completed
}

let private insertTodo (ctx: Microsoft.AspNetCore.Http.HttpContext) (userId: int) (textValue: string) =
    task {
        let db = ctx.GetService<Entity.AppDbContext>()
        let! lastTodo =
            db.Todos
                .AsNoTracking()
                .Where(fun t -> t.UserId = userId)
                .OrderByDescending(fun t -> t.Position)
                .FirstOrDefaultAsync()

        let newTodo = Entity.Todo()
        newTodo.Text <- textValue
        newTodo.Completed <- false
        let nextPosition =
            lastTodo
            |> Option.ofObj
            |> Option.map (fun todo -> todo.Position + 1)
            |> Option.defaultValue 0

        newTodo.Position <- nextPosition
        newTodo.UserId <- userId
        db.Todos.Add(newTodo) |> ignore
        let! _ = db.SaveChangesAsync()
        return newTodo
    }

let private deleteTodo (ctx: Microsoft.AspNetCore.Http.HttpContext) (userId: int) (id: int) =
    taskResult {
        let db = ctx.GetService<Entity.AppDbContext>()
        let! found =
            db.Todos.FirstOrDefaultAsync(fun t -> t.Id = id && t.UserId = userId)
            |> TaskResult.ofTask
            |> TaskResult.bind (
                Option.ofObj
                >> Result.requireSome NotFound
                >> TaskResult.ofResult
            )

        db.Todos.Remove(found) |> ignore
        let! toShift =
            db.Todos
                .Where(fun t -> t.UserId = userId && t.Position > found.Position)
                .ToListAsync()
            |> TaskResult.ofTask

        for shifted in toShift do
            shifted.Position <- shifted.Position - 1

        let! _ = db.SaveChangesAsync()
        ()
    }

let private toggleTodo (ctx: Microsoft.AspNetCore.Http.HttpContext) (userId: int) (id: int) =
    taskResult {
        let db = ctx.GetService<Entity.AppDbContext>()
        let! found =
            db.Todos.FirstOrDefaultAsync(fun t -> t.Id = id && t.UserId = userId)
            |> TaskResult.ofTask
            |> TaskResult.bind (
                Option.ofObj
                >> Result.requireSome NotFound
                >> TaskResult.ofResult
            )

        found.Completed <- not found.Completed
        let! _ = db.SaveChangesAsync()
        return found
    }

let private clearCompleted (ctx: Microsoft.AspNetCore.Http.HttpContext) (userId: int) =
    task {
        let db = ctx.GetService<Entity.AppDbContext>()
        let! completed = db.Todos.Where(fun t -> t.UserId = userId && t.Completed).ToListAsync()
        db.Todos.RemoveRange(completed)

        let! remaining =
            db.Todos
                .Where(fun t -> t.UserId = userId && not t.Completed)
                .OrderBy(fun t -> t.Position)
                .ThenBy(fun t -> t.Id)
                .ToListAsync()

        for idx = 0 to remaining.Count - 1 do
            remaining[idx].Position <- idx

        let! _ = db.SaveChangesAsync()
        ()
    }

let private moveTodo (ctx: Microsoft.AspNetCore.Http.HttpContext) (userId: int) (todoId: int) (newPosition: int) =
    taskResult {
        let db = ctx.GetService<Entity.AppDbContext>()
        let! orderedTodos =
            db.Todos
                .Where(fun t -> t.UserId = userId)
                .OrderBy(fun t -> t.Position)
                .ThenBy(fun t -> t.Id)
                .ToListAsync()

        let! oldPos =
            orderedTodos
            |> Seq.tryFindIndex (fun t -> t.Id = todoId)
            |> Result.requireSome NotFound

        let maxPosition = orderedTodos.Count - 1
        let clampedNewPosition = max 0 (min newPosition maxPosition)

        if clampedNewPosition <> oldPos then
            let movedTodo = orderedTodos[oldPos]

            if oldPos < clampedNewPosition then
                for idx = oldPos + 1 to clampedNewPosition do
                    orderedTodos[idx].Position <- orderedTodos[idx].Position - 1
            else
                for idx = clampedNewPosition to oldPos - 1 do
                    orderedTodos[idx].Position <- orderedTodos[idx].Position + 1

            movedTodo.Position <- clampedNewPosition
            let! _ = db.SaveChangesAsync()
            ()
    }

let private getTodos (ctx: Microsoft.AspNetCore.Http.HttpContext) (userId: int) =
    asyncResult {
        let db = ctx.GetService<Entity.AppDbContext>()
        let! todos =
            db.Todos
                .AsNoTracking()
                .Where(fun t -> t.UserId = userId)
                .OrderBy(fun t -> t.Position)
                .ThenBy(fun t -> t.Id)
                .ToListAsync()
            |> Async.AwaitTask
            |> AsyncResult.ofAsync

        return todos |> Seq.map toSharedTodo |> List.ofSeq
    }

let private createTodo (ctx: Microsoft.AspNetCore.Http.HttpContext) (userId: int) (payload: NewTodo) =
    asyncResult {
        do!
            payload.Text
            |> System.String.IsNullOrWhiteSpace
            |> Result.requireFalse (ValidationError "Todo text is required.")

        let! created =
            insertTodo ctx userId payload.Text
            |> Async.AwaitTask
            |> AsyncResult.ofAsync

        return toSharedTodo created
    }

let private toggleTodoById (ctx: Microsoft.AspNetCore.Http.HttpContext) (userId: int) (id: int) =
    asyncResult {
        let! updated = toggleTodo ctx userId id |> Async.AwaitTask
        return toSharedTodo updated
    }

let private deleteTodoById (ctx: Microsoft.AspNetCore.Http.HttpContext) (userId: int) (id: int) =
    asyncResult {
        do! deleteTodo ctx userId id |> Async.AwaitTask
    }

let private clearCompletedTodos (ctx: Microsoft.AspNetCore.Http.HttpContext) (userId: int) =
    asyncResult {
        do! clearCompleted ctx userId |> Async.AwaitTask |> AsyncResult.ofAsync
    }

let private moveTodoByPosition (ctx: Microsoft.AspNetCore.Http.HttpContext) (userId: int) (payload: MoveTodoRequest) =
    asyncResult {
        do!
            payload.NewPosition < 0
            |> Result.requireFalse (ValidationError "Todo position must be non-negative.")

        do! moveTodo ctx userId payload.TodoId payload.NewPosition |> Async.AwaitTask
    }

let todoApiImplementation (ctx: Microsoft.AspNetCore.Http.HttpContext) : ITodoApi =
    {
        GetTodos =
            fun () ->
                Auth.requireUser ctx <| fun userId -> getTodos ctx userId
        CreateTodo =
            fun payload ->
                Auth.requireUser ctx <| fun userId -> createTodo ctx userId payload
        ToggleTodo =
            fun id ->
                Auth.requireTodoAuthorization ctx id <| fun userId -> toggleTodoById ctx userId id
        DeleteTodo =
            fun id ->
                Auth.requireTodoAuthorization ctx id <| fun userId -> deleteTodoById ctx userId id
        ClearCompleted =
            fun () ->
                Auth.requireUser ctx <| fun userId -> clearCompletedTodos ctx userId
        MoveTodo =
            fun payload ->
                Auth.requireTodoAuthorization ctx payload.TodoId <| fun userId -> moveTodoByPosition ctx userId payload
    }

let todosApiHandler: HttpHandler =
    RemotingUtil.handlerFromApi todoApiImplementation
