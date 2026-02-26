module Data

open System.Linq
open BCrypt.Net
open Entity
open Dtos
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
            let devUser = {
                Id = 0
                Email = email
                PasswordHash = passwordHash
            }
            db.Users.Add(devUser) |> ignore
            let! _ = db.SaveChangesAsync()
            ()
    }

let findUserByEmail (db: AppDbContext) (email: string) =
    db.Users.FirstOrDefaultAsync(fun u -> u.Email = email)

let getAllTodos (db: AppDbContext) (userId: int) =
    db.Todos.Where(fun t -> t.UserId = userId).OrderBy(fun t -> t.Id).ToListAsync()

let insertTodo (db: AppDbContext) (userId: int) (textValue: string) =
    task {
        let newTodo = {
            Id = 0
            Text = textValue
            Completed = false
            UserId = userId
        }
        db.Todos.Add(newTodo) |> ignore
        let! _ = db.SaveChangesAsync()
        return newTodo
    }

let updateTodo (db: AppDbContext) (userId: int) (id: int) (patch: UpdateTodo) =
    task {
        let! todo = db.Todos.FindAsync(id).AsTask() |> Async.AwaitTask

        match Option.ofObj todo with
        | None -> return None
        | Some todo ->
            if todo.UserId <> userId then
                return None
            else
                patch.text |> Option.iter (fun text -> todo.Text <- text)
                patch.completed |> Option.iter (fun completed -> todo.Completed <- completed)
                let! _ = db.SaveChangesAsync()
                return Some todo
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
                db.Todos.Remove(todo) |> ignore
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
        let completed = db.Todos.Where(fun t -> t.UserId = userId && t.Completed)
        db.Todos.RemoveRange(completed)
        let! _ = db.SaveChangesAsync()
        return ()
    }
