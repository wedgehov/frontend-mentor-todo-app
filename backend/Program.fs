open System
open System.Text.Json
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Http.Json
open Npgsql

// DTOs
type Todo    = { Id: int; Text: string; Completed: bool }
type NewTodo = { Text: string }

let builder = WebApplication.CreateBuilder()

// Option A: explicit type for JSON options (fixes FS0072)
let configureJson (o: JsonOptions) =
    o.SerializerOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    o.SerializerOptions.PropertyNameCaseInsensitive <- true

builder.Services.Configure<JsonOptions>(configureJson) |> ignore

// Connection + data source (keeps a pool for the app lifetime)
let cfg = builder.Configuration
let connStr = cfg.GetConnectionString("DefaultConnection")
let dataSource = NpgsqlDataSourceBuilder(connStr).Build()

// Idempotent startup migration (synchronous)
let ensureTables () =
    use cmd = dataSource.CreateCommand("""
        CREATE TABLE IF NOT EXISTS todos (
            id SERIAL PRIMARY KEY,
            text VARCHAR(255) NOT NULL,
            completed BOOLEAN NOT NULL DEFAULT FALSE
        );
        CREATE INDEX IF NOT EXISTS ix_todos_completed ON todos(completed);
    """)
    cmd.ExecuteNonQuery() |> ignore

// Handlers (synchronous: simple + avoids task CE issues)
let getTodos () : IResult =
    use cmd = dataSource.CreateCommand("select id, text, completed from todos order by id;")
    use r = cmd.ExecuteReader()
    let acc = ResizeArray<Todo>()
    while r.Read() do
        acc.Add({ Id = r.GetInt32(0); Text = r.GetString(1); Completed = r.GetBoolean(2) })
    Results.Ok(acc)

let postTodo (todo: NewTodo) : IResult =
    if String.IsNullOrWhiteSpace(todo.Text) then
        Results.BadRequest("Text is required")
    else
        use cmd = dataSource.CreateCommand(
            "insert into todos(text, completed) values ($1, false) returning id, text, completed;")
        cmd.Parameters.AddWithValue(todo.Text) |> ignore
        use r = cmd.ExecuteReader()
        if not (r.Read()) then Results.Problem("Insert failed")
        else
            let created = { Id = r.GetInt32(0); Text = r.GetString(1); Completed = r.GetBoolean(2) }
            Results.Created($"/api/todos/{created.Id}", created)

let toggleTodo (id:int) : IResult =
    use cmd = dataSource.CreateCommand(
        "update todos set completed = not completed where id = $1 returning id, text, completed;")
    cmd.Parameters.AddWithValue(id) |> ignore
    use r = cmd.ExecuteReader()
    if not (r.Read()) then Results.NotFound($"Todo {id} not found")
    else
        let t = { Id = r.GetInt32(0); Text = r.GetString(1); Completed = r.GetBoolean(2) }
        Results.Ok(t)

let deleteTodo (id:int) : IResult =
    use cmd = dataSource.CreateCommand("delete from todos where id = $1;")
    cmd.Parameters.AddWithValue(id) |> ignore
    let rows = cmd.ExecuteNonQuery()
    if rows = 0 then Results.NotFound($"Todo {id} not found") else Results.NoContent()

let clearCompleted () : IResult =
    use cmd = dataSource.CreateCommand("delete from todos where completed = true;")
    cmd.ExecuteNonQuery() |> ignore
    Results.NoContent()

let app = builder.Build()

// Ensure schema before serving
ensureTables ()

// Health + routes
app.MapGet ("/api/health",                 Func<IResult>(fun () -> Results.Text "OK"))      |> ignore
app.MapGet ("/api/todos",                  Func<IResult>(getTodos))                         |> ignore
app.MapPost("/api/todos",                  Func<NewTodo, IResult>(postTodo))               |> ignore
app.MapPut ("/api/todos/{id:int}/toggle",  Func<int, IResult>(toggleTodo))                  |> ignore
app.MapDelete("/api/todos/{id:int}",       Func<int, IResult>(deleteTodo))                   |> ignore
app.MapDelete("/api/todos/completed",      Func<IResult>(clearCompleted))                   |> ignore

app.Run()
