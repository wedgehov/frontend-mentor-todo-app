module Backend.Program

open System
open System.Linq
open System.ComponentModel.DataAnnotations.Schema
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.EntityFrameworkCore
open Microsoft.AspNetCore.Http
open Giraffe
open OpenTelemetry.Metrics
open OpenTelemetry.Trace
open Npgsql

// =====================
// DTOs (wire format)
// =====================

[<CLIMutable>]
[<Table("todos")>]
type Todo =
    { [<Column("id")>] id : int
      mutable text      : string
      mutable completed : bool }

[<CLIMutable>]
type NewTodo = { text : string }

[<CLIMutable>]
type UpdateTodo =
    { text      : string option
      completed : bool   option }

// =====================
// EF Core DbContext
// =====================

type AppDbContext(options: DbContextOptions<AppDbContext>) =
    inherit DbContext(options)
    [<DefaultValue>] val mutable private todos : DbSet<Todo>
    member this.Todos with get() = this.todos and set(v) = this.todos <- v

// =====================
// DB helpers
// =====================

let getAllTodos (db : AppDbContext) =
    db.Todos.OrderBy(fun t -> t.id).ToListAsync()

let insertTodo (db : AppDbContext) (textValue : string) = task {
    let newTodo = { id = 0; text = textValue; completed = false }
    db.Todos.Add(newTodo) |> ignore
    let! _ = db.SaveChangesAsync()
    return newTodo
}

let updateTodo (db : AppDbContext) (id : int) (patch : UpdateTodo) = task {
    let! todo = db.Todos.FindAsync(id).AsTask() |> Async.AwaitTask
    match Option.ofObj todo with
    | None -> return None
    | Some todo ->
        patch.text      |> Option.iter (fun t -> todo.text <- t)
        patch.completed |> Option.iter (fun c -> todo.completed <- c)
        let! _ = db.SaveChangesAsync()
        return Some todo
}

let deleteTodo (db : AppDbContext) (id : int) = task {
    let! todo = db.Todos.FindAsync(id).AsTask() |> Async.AwaitTask
    match Option.ofObj todo with
    | None -> return false
    | Some todo ->
        db.Todos.Remove(todo) |> ignore
        let! count = db.SaveChangesAsync()
        return count > 0
}

let toggleTodo (db : AppDbContext) (id : int) = task {
    let! todo = db.Todos.FindAsync(id).AsTask() |> Async.AwaitTask
    match Option.ofObj todo with
    | None -> return None
    | Some todo ->
        todo.completed <- not todo.completed
        let! _ = db.SaveChangesAsync()
        return Some todo
}

let clearCompleted (db : AppDbContext) = task {
    let completed = db.Todos.Where(fun t -> t.completed)
    db.Todos.RemoveRange(completed)
    let! _ = db.SaveChangesAsync()
    return ()
}

// =====================
// HTTP Handlers (Giraffe)
// =====================

let handleGetTodos : HttpHandler = fun next ctx ->
    let db = ctx.GetService<AppDbContext>()
    try
        task {
            let! todos = getAllTodos db
            return! json todos next ctx
        }
    with ex ->
        (setStatusCode 500 >=> text ("Internal Server Error: " + ex.Message)) next ctx

let handleCreateTodo : HttpHandler = fun next ctx -> task {
    let db = ctx.GetService<AppDbContext>()
    try
        let! payload = ctx.BindJsonAsync<NewTodo>()
        if isNull (box payload) || String.IsNullOrWhiteSpace payload.text then
            return! (RequestErrors.badRequest (text "text is required")) next ctx
        else
            let! created = insertTodo db payload.text
            return! (setStatusCode 201
                     >=> setHttpHeader "Location" (sprintf "/api/todos/%d" created.id)
                     >=> json created) next ctx
    with ex ->
        return! (ServerErrors.internalError (text ("Internal Server Error: " + ex.Message))) next ctx
}

let handlePatchTodo (id:int) : HttpHandler = fun next ctx -> task {
    let db = ctx.GetService<AppDbContext>()
    try
        let! patch = ctx.BindJsonAsync<UpdateTodo>()
        let! updated = updateTodo db id patch
        match updated with
        | Some t -> return! json t next ctx
        | None   -> return! RequestErrors.notFound (text "Not found") next ctx
    with ex ->
        return! (ServerErrors.internalError (text ("Internal Server Error: " + ex.Message))) next ctx
}

let handleDeleteTodo (id:int) : HttpHandler = fun next ctx -> task {
    let db = ctx.GetService<AppDbContext>()
    let! ok = deleteTodo db id
    if ok then return! (setStatusCode 204 >=> text "") next ctx
    else     return! RequestErrors.notFound (text "Not found") next ctx
}

let handleToggleTodo (id:int) : HttpHandler = fun next ctx -> task {
    let db = ctx.GetService<AppDbContext>()
    let! res = toggleTodo db id
    match res with
    | Some t -> return! json t next ctx
    | None   -> return! RequestErrors.notFound (text "Not found") next ctx
}

let handleClearCompleted : HttpHandler = fun next ctx -> task {
    let db = ctx.GetService<AppDbContext>()
    do! clearCompleted db
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

    // Configure OpenTelemetry for metrics and tracing
    builder.Services.AddOpenTelemetry()
        |> fun otel -> otel.WithMetrics(fun metrics ->
            metrics
                // For metrics, we add a 'View' to include the 'http.route' tag.
                .AddView("http.server.request.duration", new MetricStreamConfiguration(TagKeys = [| "http.route" |]))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                // Collect .NET runtime metrics (GC, JIT, etc.)
                .AddRuntimeInstrumentation()
                // Expose a /metrics endpoint for Prometheus to scrape
                .AddPrometheusExporter()
            |> ignore
        )
        |> fun otel -> otel.WithTracing(fun tracing ->
            // For tracing, we use the EnrichWithHttpRequest option.
            tracing
                .AddAspNetCoreInstrumentation(fun (opts: OpenTelemetry.Instrumentation.AspNetCore.AspNetCoreTraceInstrumentationOptions) -> (
                    opts.EnrichWithHttpRequest <- Action<System.Diagnostics.Activity, Microsoft.AspNetCore.Http.HttpRequest>(fun activity req ->
                        match req.RouteValues.TryGetValue "page" with // "page" is from Giraffe's route parsing
                        | true, value -> activity.AddTag("http.route", value) |> ignore // Add route tag for better cardinality in traces
                        | _ -> ())
                ))
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddEntityFrameworkCoreInstrumentation(fun efCoreOpts -> efCoreOpts.SetDbStatementForText <- true)
                // Export traces to an OTLP collector (like Grafana Agent, Jaeger, etc.)
                // The endpoint is configured via OTEL_EXPORTER_OTLP_ENDPOINT env var.
                .AddOtlpExporter()
            |> ignore
        ) |> ignore

    // Connection string must be provided:
    // - appsettings.json:  ConnectionStrings:DefaultConnection
    // - or env var:        DOTNET_ConnectionStrings__DefaultConnection
    builder.Services.AddSingleton<NpgsqlDataSource>(fun sp ->
        let cfg = sp.GetRequiredService<IConfiguration>()
        let connStr = cfg.GetConnectionString("DefaultConnection")
        let env = sp.GetRequiredService<IHostEnvironment>()
        let loggerFactory = sp.GetRequiredService<ILoggerFactory>()
        let dataSourceBuilder = NpgsqlDataSourceBuilder(connStr)
        dataSourceBuilder.UseLoggerFactory(loggerFactory) |> ignore
        // Log parameter values in development for easier debugging
        if env.IsDevelopment() then
            dataSourceBuilder.EnableParameterLogging() |> ignore
        dataSourceBuilder.Build()
    ) |> ignore

    builder.Services.AddDbContext<AppDbContext>(fun (sp: IServiceProvider) (options: DbContextOptionsBuilder) ->
        let dataSource = sp.GetRequiredService<NpgsqlDataSource>()
        options.UseNpgsql(dataSource, fun npgsqlOptions ->
            // If you wanted to use migrations, you would specify the assembly here
            () // This lambda needs a body; () is the unit value.
        ) |> ignore
    ) |> ignore

    builder.Services.AddGiraffe() |> ignore

    let app = builder.Build()

    // In a real app, you might use a scope to get the DbContext
    // For this simple startup task, getting it directly is fine.
    use scope = app.Services.CreateScope()
    let dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>()
    // This ensures the database is created, but doesn't handle schema updates.
    // For production, `dbContext.Database.MigrateAsync()` is preferred.
    dbContext.Database.EnsureCreated() |> ignore

    // Add the /metrics endpoint for Prometheus
    app.UseOpenTelemetryPrometheusScrapingEndpoint() |> ignore

    app.UseGiraffe webApp |> ignore
    app.Run()
    0
