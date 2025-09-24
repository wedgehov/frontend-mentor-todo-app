module Backend.Program

open System
open System.Data
open System.Data.Common
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Giraffe
open Npgsql

// =====================
// DTOs (wire format)
// =====================

[<CLIMutable>]
type Todo =
    { id        : int
      text      : string
      completed : bool }

[<CLIMutable>]
type NewTodo = { text : string }

[<CLIMutable>]
type UpdateTodo =
    { text      : string option
      completed : bool   option }

// =====================
// DB helpers
// =====================

let ensureTables (ds : NpgsqlDataSource) = task {
    use! conn = ds.OpenConnectionAsync()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- """
        create table if not exists todos (
            id serial primary key,
            "text"     text    not null,
            completed  boolean not null default false
        );
        -- idempotent guards
        alter table if exists todos add column if not exists "text" text not null default '';
        alter table if exists todos add column if not exists completed boolean not null default false;
    """
    let! _ = cmd.ExecuteNonQueryAsync()
    return ()
}

let private readTodo (reader : DbDataReader) : Todo =
    { id        = reader.GetInt32(0)
      text      = reader.GetString(1)
      completed = reader.GetBoolean(2) }

let getAllTodos (ds : NpgsqlDataSource) = task {
    use! conn = ds.OpenConnectionAsync()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "select id, \"text\" as text, completed from todos order by id;"
    use! reader = cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess)
    let results = ResizeArray<Todo>()
    while reader.Read() do
        results.Add(readTodo reader)
    return List.ofSeq results
}

let insertTodo (ds : NpgsqlDataSource) (textValue : string) = task {
    use! conn = ds.OpenConnectionAsync()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "insert into todos (\"text\") values ($1) returning id, \"text\" as text, completed;"
    cmd.Parameters.AddWithValue(textValue) |> ignore
    use! reader = cmd.ExecuteReaderAsync()
    let! _ = reader.ReadAsync()
    return readTodo reader
}

let updateTodo (ds : NpgsqlDataSource) (id : int) (patch : UpdateTodo) = task {
    use! conn = ds.OpenConnectionAsync()
    use cmd = conn.CreateCommand()

    let setters = ResizeArray<string>()
    let values  = ResizeArray<obj>()

    match patch.text with
    | Some t ->
        setters.Add("\"text\" = $1")
        values.Add(box t)
    | None -> ()

    match patch.completed with
    | Some c ->
        let pos = values.Count + 1
        setters.Add(sprintf "completed = $%d" pos)
        values.Add(box c)
    | None -> ()

    if setters.Count = 0 then
        // nothing to update -> return current row if exists
        use cmd2 = conn.CreateCommand()
        cmd2.CommandText <- "select id, \"text\" as text, completed from todos where id = $1;"
        cmd2.Parameters.AddWithValue(id) |> ignore
        use! reader = cmd2.ExecuteReaderAsync()
        let! has = reader.ReadAsync()
        if not has then return None
        else return Some (readTodo reader)
    else
        let setClause = String.concat ", " setters
        cmd.CommandText <-
            sprintf "update todos set %s where id = $%d returning id, \"text\" as text, completed;"
                    setClause (values.Count + 1)

        for v in values do cmd.Parameters.AddWithValue(v) |> ignore
        cmd.Parameters.AddWithValue(id) |> ignore

        use! reader = cmd.ExecuteReaderAsync()
        let! has = reader.ReadAsync()
        if not has then return None
        else return Some (readTodo reader)
}

let deleteTodo (ds : NpgsqlDataSource) (id : int) = task {
    use! conn = ds.OpenConnectionAsync()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "delete from todos where id = $1;"
    cmd.Parameters.AddWithValue(id) |> ignore
    let! n = cmd.ExecuteNonQueryAsync()
    return n > 0
}

let toggleTodo (ds : NpgsqlDataSource) (id : int) = task {
    use! conn = ds.OpenConnectionAsync()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "update todos set completed = not completed where id = $1 returning id, \"text\" as text, completed;"
    cmd.Parameters.AddWithValue(id) |> ignore
    use! reader = cmd.ExecuteReaderAsync()
    let! has = reader.ReadAsync()
    if not has then return None
    else return Some (readTodo reader)
}

let clearCompleted (ds : NpgsqlDataSource) = task {
    use! conn = ds.OpenConnectionAsync()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "delete from todos where completed = true;"
    let! _ = cmd.ExecuteNonQueryAsync()
    return ()
}

// =====================
// HTTP Handlers (Giraffe)
// =====================

let handleGetTodos : HttpHandler = fun next ctx -> task {
    let ds = ctx.GetService<NpgsqlDataSource>()
    try
        let! todos = getAllTodos ds
        return! json todos next ctx
    with ex ->
        return! (setStatusCode 500 >=> text ("Internal Server Error: " + ex.Message)) next ctx
}

let handleCreateTodo : HttpHandler = fun next ctx -> task {
    let ds = ctx.GetService<NpgsqlDataSource>()
    try
        let! payload = ctx.BindJsonAsync<NewTodo>()
        if isNull (box payload) || String.IsNullOrWhiteSpace payload.text then
            return! (RequestErrors.badRequest (text "text is required")) next ctx
        else
            let! created = insertTodo ds payload.text
            return! (setStatusCode 201
                     >=> setHttpHeader "Location" (sprintf "/api/todos/%d" created.id)
                     >=> json created) next ctx
    with ex ->
        return! (ServerErrors.internalError (text ("Internal Server Error: " + ex.Message))) next ctx
}

let handlePatchTodo (id:int) : HttpHandler = fun next ctx -> task {
    let ds = ctx.GetService<NpgsqlDataSource>()
    try
        let! patch = ctx.BindJsonAsync<UpdateTodo>()
        let! updated = updateTodo ds id patch
        match updated with
        | Some t -> return! json t next ctx
        | None   -> return! RequestErrors.notFound (text "Not found") next ctx
    with ex ->
        return! (ServerErrors.internalError (text ("Internal Server Error: " + ex.Message))) next ctx
}

let handleDeleteTodo (id:int) : HttpHandler = fun next ctx -> task {
    let ds = ctx.GetService<NpgsqlDataSource>()
    let! ok = deleteTodo ds id
    if ok then return! (setStatusCode 204 >=> text "") next ctx
    else     return! RequestErrors.notFound (text "Not found") next ctx
}

let handleToggleTodo (id:int) : HttpHandler = fun next ctx -> task {
    let ds = ctx.GetService<NpgsqlDataSource>()
    let! res = toggleTodo ds id
    match res with
    | Some t -> return! json t next ctx
    | None   -> return! RequestErrors.notFound (text "Not found") next ctx
}

let handleClearCompleted : HttpHandler = fun next ctx -> task {
    let ds = ctx.GetService<NpgsqlDataSource>()
    do! clearCompleted ds
    return! (setStatusCode 204 >=> text "") next ctx
}

// =====================
// Routes
// =====================

let webApp : HttpHandler =
    choose [
        route  "/" >=> text "OK"
        subRoute "/api/todos" (
            choose [
                GET    >=> route ""            >=> handleGetTodos
                POST   >=> route ""            >=> handleCreateTodo
                PUT    >=> routef "/%i/toggle"    handleToggleTodo
                DELETE >=> routef "/%i"           handleDeleteTodo
                DELETE >=> route  "/completed" >=> handleClearCompleted
                PATCH  >=> routef "/%i"           handlePatchTodo
            ]
        )
    ]

// =====================
// App bootstrap
// =====================

[<EntryPoint>]
let main argv =
    let builder = WebApplication.CreateBuilder(argv)

    // Use simple console logging in development for readability,
    // and JSON logging in production for structured data.
    builder.Logging.ClearProviders() |> ignore
    if builder.Environment.IsDevelopment() then
        builder.Logging.AddConsole() |> ignore
    else
        builder.Logging.AddJsonConsole() |> ignore

    // Connection string must be provided:
    // - appsettings.json:  ConnectionStrings:DefaultConnection
    // - or env var:        DOTNET_ConnectionStrings__DefaultConnection
    builder.Services.AddSingleton<NpgsqlDataSource>(fun sp ->
        let env = sp.GetRequiredService<IHostEnvironment>()
        let cfg = sp.GetRequiredService<IConfiguration>()
        let connStr = cfg.GetConnectionString("DefaultConnection")
        let loggerFactory = sp.GetRequiredService<ILoggerFactory>()
        let dataSourceBuilder = NpgsqlDataSourceBuilder(connStr)
        dataSourceBuilder.UseLoggerFactory(loggerFactory) |> ignore
        // Log parameter values in development for easier debugging
        if env.IsDevelopment() then
            dataSourceBuilder.EnableParameterLogging() |> ignore
        dataSourceBuilder.Build()
    ) |> ignore

    builder.Services.AddGiraffe() |> ignore

    let app = builder.Build()

    // one-time migration
    app.Services.GetRequiredService<NpgsqlDataSource>()
    |> ensureTables
    |> Async.AwaitTask
    |> Async.RunSynchronously

    app.UseGiraffe webApp
    app.Run()
    0
