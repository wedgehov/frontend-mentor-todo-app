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

let private insertTodo (db: Entity.AppDbContext) (userId: int) (textValue: string) =
    task {
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

let private deleteTodo (db: Entity.AppDbContext) (userId: int) (id: int) =
    taskResult {
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

let private toggleTodo (db: Entity.AppDbContext) (userId: int) (id: int) =
    taskResult {
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

let private clearCompleted (db: Entity.AppDbContext) (userId: int) =
    task {
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

let private moveTodo (db: Entity.AppDbContext) (userId: int) (todoId: int) (newPosition: int) =
    taskResult {
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

let private getTodos (ctx: Microsoft.AspNetCore.Http.HttpContext) (db: Entity.AppDbContext) () =
    asyncResult {
        let! userId = ctx |> Auth.tryGetUserId |> AsyncResult.ofResult
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

let private createTodo (ctx: Microsoft.AspNetCore.Http.HttpContext) (db: Entity.AppDbContext) (payload: NewTodo) =
    asyncResult {
        let! userId = ctx |> Auth.tryGetUserId |> AsyncResult.ofResult

        do!
            payload.Text
            |> System.String.IsNullOrWhiteSpace
            |> Result.requireFalse (ValidationError "Todo text is required.")

        let! created =
            insertTodo db userId payload.Text
            |> Async.AwaitTask
            |> AsyncResult.ofAsync

        return toSharedTodo created
    }

let private toggleTodoById (ctx: Microsoft.AspNetCore.Http.HttpContext) (db: Entity.AppDbContext) (id: int) =
    asyncResult {
        let! userId = ctx |> Auth.tryGetUserId |> AsyncResult.ofResult
        let! updated = toggleTodo db userId id |> Async.AwaitTask
        return toSharedTodo updated
    }

let private deleteTodoById (ctx: Microsoft.AspNetCore.Http.HttpContext) (db: Entity.AppDbContext) (id: int) =
    asyncResult {
        let! userId = ctx |> Auth.tryGetUserId |> AsyncResult.ofResult
        do! deleteTodo db userId id |> Async.AwaitTask
    }

let private clearCompletedTodos (ctx: Microsoft.AspNetCore.Http.HttpContext) (db: Entity.AppDbContext) () =
    asyncResult {
        let! userId = ctx |> Auth.tryGetUserId |> AsyncResult.ofResult
        do! clearCompleted db userId |> Async.AwaitTask |> AsyncResult.ofAsync
    }

let private moveTodoByPosition (ctx: Microsoft.AspNetCore.Http.HttpContext) (db: Entity.AppDbContext) (payload: MoveTodoRequest) =
    asyncResult {
        let! userId = ctx |> Auth.tryGetUserId |> AsyncResult.ofResult

        do!
            payload.NewPosition < 0
            |> Result.requireFalse (ValidationError "Todo position must be non-negative.")

        do! moveTodo db userId payload.TodoId payload.NewPosition |> Async.AwaitTask
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
