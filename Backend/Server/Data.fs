module Data

open System.Linq
open BCrypt.Net
open Entity
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.Extensions.Logging

let seedDevelopmentData (db: AppDbContext) =
    task {
        let email = "test@example.com"
        let! existingUser = db.Users.FirstOrDefaultAsync(fun u -> u.Email = email)

        if existingUser = null then
            let logger = db.GetService<ILogger<AppDbContext>>()
            logger.LogInformation("Seeding development user '{Email}'", email)
            let passwordHash = BCrypt.HashPassword("secret123")
            let devUser = User()
            devUser.Email <- email
            devUser.PasswordHash <- passwordHash
            db.Users.Add(devUser) |> ignore
            let! _ = db.SaveChangesAsync()
            ()
    }

let findUserByEmail (db: AppDbContext) (email: string) =
    db.Users.FirstOrDefaultAsync(fun u -> u.Email = email)

let getAllTodos (db: AppDbContext) (userId: int) =
    db.Todos.Where(fun t -> t.UserId = userId).OrderBy(fun t -> t.Position).ThenBy(fun t -> t.Id).ToListAsync()

let insertTodo (db: AppDbContext) (userId: int) (textValue: string) =
    task {
        let! lastTodo =
            db.Todos
                .Where(fun t -> t.UserId = userId)
                .OrderByDescending(fun t -> t.Position)
                .FirstOrDefaultAsync()

        let newTodo = Todo()
        newTodo.Text <- textValue
        newTodo.Completed <- false
        let nextPosition =
            match Option.ofObj lastTodo with
            | None -> 0
            | Some todo -> todo.Position + 1

        newTodo.Position <- nextPosition
        newTodo.UserId <- userId
        db.Todos.Add(newTodo) |> ignore
        let! _ = db.SaveChangesAsync()
        return newTodo
    }

let deleteTodo (db: AppDbContext) (userId: int) (id: int) =
    task {
        let! todo = db.Todos.FindAsync(id).AsTask() |> Async.AwaitTask

        match Option.ofObj todo with
        | None -> return false
        | Some todo ->
            if todo.UserId <> userId then
                return false
            else
                let deletedPosition = todo.Position
                db.Todos.Remove(todo) |> ignore
                let! toShift =
                    db.Todos
                        .Where(fun t -> t.UserId = userId && t.Position > deletedPosition)
                        .ToListAsync()

                for shifted in toShift do
                    shifted.Position <- shifted.Position - 1

                let! count = db.SaveChangesAsync()
                return count > 0
    }

let toggleTodo (db: AppDbContext) (userId: int) (id: int) =
    task {
        let! todo = db.Todos.FindAsync(id).AsTask() |> Async.AwaitTask

        match Option.ofObj todo with
        | None -> return None
        | Some todo ->
            if todo.UserId <> userId then
                return None
            else
                todo.Completed <- not todo.Completed
                let! _ = db.SaveChangesAsync()
                return Some todo
    }

let clearCompleted (db: AppDbContext) (userId: int) =
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
        return ()
    }

let moveTodo (db: AppDbContext) (userId: int) (todoId: int) (newPosition: int) =
    task {
        let! orderedTodos =
            db.Todos
                .Where(fun t -> t.UserId = userId)
                .OrderBy(fun t -> t.Position)
                .ThenBy(fun t -> t.Id)
                .ToListAsync()

        let oldPosition =
            orderedTodos
            |> Seq.tryFindIndex (fun t -> t.Id = todoId)

        match oldPosition with
        | None -> return false
        | Some oldPos ->
            let maxPosition = orderedTodos.Count - 1
            let clampedNewPosition = max 0 (min newPosition maxPosition)

            if clampedNewPosition = oldPos then
                return true
            else
                let movedTodo = orderedTodos[oldPos]

                if oldPos < clampedNewPosition then
                    for idx = oldPos + 1 to clampedNewPosition do
                        orderedTodos[idx].Position <- orderedTodos[idx].Position - 1
                else
                    for idx = clampedNewPosition to oldPos - 1 do
                        orderedTodos[idx].Position <- orderedTodos[idx].Position + 1

                movedTodo.Position <- clampedNewPosition
                let! _ = db.SaveChangesAsync()
                return true
    }
