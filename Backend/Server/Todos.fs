module Todos

open System
open Data
open Dtos
open Entity
open Giraffe
open Microsoft.Extensions.DependencyInjection

let handleGetTodos: HttpHandler =
    fun next ctx ->
        let db = ctx.GetService<AppDbContext>()
        let userId = Auth.getUserId ctx

        task {
            let! todos = getAllTodos db userId
            return! json todos next ctx
        }

let handleCreateTodo: HttpHandler =
    fun next ctx ->
        task {
            let db = ctx.GetService<AppDbContext>()
            let userId = Auth.getUserId ctx
            let! payload = ctx.BindJsonAsync<NewTodo>()

            if String.IsNullOrWhiteSpace payload.text then
                return! (setStatusCode 400 >=> text "text is required") next ctx
            else
                let! created = insertTodo db userId payload.text

                return!
                    (setStatusCode 201
                     >=> setHttpHeader "Location" (sprintf "/api/todos/%d" created.Id)
                     >=> json created)
                        next
                        ctx
        }

let handlePatchTodo (id: int) : HttpHandler =
    fun next ctx ->
        task {
            let db = ctx.GetService<AppDbContext>()
            let userId = Auth.getUserId ctx
            let! patch = ctx.BindJsonAsync<UpdateTodo>()
            let! updated = updateTodo db userId id patch

            match updated with
            | Some todo -> return! json todo next ctx
            | None -> return! (setStatusCode 404 >=> text "Not found") next ctx
        }

let handleDeleteTodo (id: int) : HttpHandler =
    fun next ctx ->
        task {
            let db = ctx.GetService<AppDbContext>()
            let userId = Auth.getUserId ctx
            let! ok = deleteTodo db userId id

            if ok then
                return! (setStatusCode 204 >=> text "") next ctx
            else
                return! (setStatusCode 404 >=> text "Not found") next ctx
        }

let handleToggleTodo (id: int) : HttpHandler =
    fun next ctx ->
        task {
            let db = ctx.GetService<AppDbContext>()
            let userId = Auth.getUserId ctx
            let! res = toggleTodo db userId id

            match res with
            | Some todo -> return! json todo next ctx
            | None -> return! (setStatusCode 404 >=> text "Not found") next ctx
        }

let handleClearCompleted: HttpHandler =
    fun next ctx ->
        task {
            let db = ctx.GetService<AppDbContext>()
            let userId = Auth.getUserId ctx
            do! clearCompleted db userId
            return! (setStatusCode 204 >=> text "") next ctx
        }
