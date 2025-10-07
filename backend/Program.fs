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
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open System.Security.Claims
open BCrypt.Net
open Microsoft.EntityFrameworkCore.Infrastructure
open System.IO
open System.Runtime.Loader

// =====================
// DTOs (wire format)
// =====================

[<CLIMutable>]
[<Table("users")>]
type User =
    { [<Column("id")>] id : int
      [<Column("email")>] email : string
      [<Column("password_hash")>] passwordHash : string }

[<CLIMutable>]
[<Table("todos")>]
type Todo =
    { [<Column("id")>] id : int
      mutable text      : string
      mutable completed : bool
      [<Column("user_id")>] userId : int }

[<CLIMutable>]
type NewTodo = { text : string }

[<CLIMutable>]
type UpdateTodo =
    { text      : string option
      completed : bool   option }

[<CLIMutable>]
type RegisterUserRequest =
    { email    : string
      password : string }

[<CLIMutable>]
type LoginUserRequest =
    { email    : string
      password : string }

// =====================
// EF Core DbContext
// =====================

type AppDbContext(options: DbContextOptions<AppDbContext>) =
    inherit DbContext(options)
    [<DefaultValue(false)>] val mutable private todos : DbSet<Todo>
    member this.Todos with get() = this.todos and set(v) = this.todos <- v
    [<DefaultValue(false)>] val mutable private users : DbSet<User>
    member this.Users with get() = this.users and set(v) = this.users <- v
    
    override this.OnModelCreating(modelBuilder: ModelBuilder) =
        // Configure User entity
        modelBuilder.Entity<User>().ToTable("users") |> ignore
        modelBuilder.Entity<User>().HasKey("id") |> ignore
        modelBuilder.Entity<User>().Property(fun u -> u.id).ValueGeneratedOnAdd() |> ignore
        
        // Configure Todo entity  
        modelBuilder.Entity<Todo>().ToTable("todos") |> ignore
        modelBuilder.Entity<Todo>().HasKey("id") |> ignore
        modelBuilder.Entity<Todo>().Property(fun t -> t.id).ValueGeneratedOnAdd() |> ignore
        
        base.OnModelCreating(modelBuilder)

// =====================
// DB helpers
// =====================

let seedDevelopmentData (db: AppDbContext) = task {
    // This function is idempotent. It won't fail if the user already exists.
    let email = "test@example.com"
    let! existingUser = db.Users.FirstOrDefaultAsync(fun u -> u.email = email)
    if existingUser = null then
        let logger = db.GetService<ILogger<AppDbContext>>()
        logger.LogInformation("Seeding development user '{Email}'", email)
        // Password is "secret123"
        let passwordHash = BCrypt.HashPassword("secret123")
        let devUser = { id = 0; email = email; passwordHash = passwordHash }
        db.Users.Add(devUser) |> ignore
        let! _ = db.SaveChangesAsync()
        ()
}

let findUserByEmail (db : AppDbContext) (email : string) =
    db.Users.FirstOrDefaultAsync(fun u -> u.email = email)

let getAllTodos (db : AppDbContext) (userId : int) =
    db.Todos.Where(fun t -> t.userId = userId).OrderBy(fun t -> t.id).ToListAsync()

let insertTodo (db : AppDbContext) (userId : int) (textValue : string) = task {
    let newTodo = { id = 0; text = textValue; completed = false; userId = userId }
    db.Todos.Add(newTodo) |> ignore
    let! _ = db.SaveChangesAsync()
    return newTodo
}

let updateTodo (db : AppDbContext) (userId : int) (id : int) (patch : UpdateTodo) = task {
    let! todo = db.Todos.FindAsync(id).AsTask() |> Async.AwaitTask
    match Option.ofObj todo with
    | None -> return None
    | Some todo ->
        if todo.userId <> userId then return None else
        patch.text      |> Option.iter (fun t -> todo.text <- t)
        patch.completed |> Option.iter (fun c -> todo.completed <- c)
        let! _ = db.SaveChangesAsync()
        return Some todo
}

let deleteTodo (db : AppDbContext) (userId : int) (id : int) = task {
    let! todo = db.Todos.FindAsync(id).AsTask() |> Async.AwaitTask
    match Option.ofObj todo with
    | None -> return false
    | Some todo ->
        if todo.userId <> userId then return false else
        db.Todos.Remove(todo) |> ignore
        let! count = db.SaveChangesAsync()
        return count > 0
}

let toggleTodo (db : AppDbContext) (userId : int) (id : int) = task {
    let! todo = db.Todos.FindAsync(id).AsTask() |> Async.AwaitTask
    match Option.ofObj todo with
    | None -> return None
    | Some todo ->
        if todo.userId <> userId then return None else
        todo.completed <- not todo.completed
        let! _ = db.SaveChangesAsync()
        return Some todo
}

let clearCompleted (db : AppDbContext) (userId : int) = task {
    let completed = db.Todos.Where(fun t -> t.userId = userId && t.completed)
    db.Todos.RemoveRange(completed)
    let! _ = db.SaveChangesAsync()
    return ()
}

// =====================
// HTTP Handlers (Giraffe)
// =====================

let requiresAuthentication : HttpHandler =
    fun next ctx ->
        let isAuthenticated =
            match ctx.User with
            | null -> false
            | user ->
                match user.Identity with
                | null -> false
                | identity -> identity.IsAuthenticated
        if isAuthenticated then
            next ctx
        else
            (setStatusCode 401 >=> text "User not authenticated.") next ctx

let getUserId (ctx: HttpContext) : int =
    match ctx.User with
    | null -> failwith "getUserId was called on an unauthenticated HttpContext. This indicates a programming error where an authenticated route did not use the 'requiresAuthentication' handler."
    | user ->
        match user.FindFirst "UserId" with
        | null -> failwith "UserId claim not found in token. This is unexpected for an authenticated user."
        | claim -> Int32.Parse claim.Value

let handleGetTodos : HttpHandler = fun next ctx ->
    let db = ctx.GetService<AppDbContext>()
    let userId = getUserId ctx
    task {
        let! todos = getAllTodos db userId
        return! json todos next ctx
    }

let handleCreateTodo : HttpHandler = fun next ctx -> task {
    let db = ctx.GetService<AppDbContext>()
    let userId = getUserId ctx
    let! payload = ctx.BindJsonAsync<NewTodo>()
    if payload = null || String.IsNullOrWhiteSpace payload.text then
        return! (setStatusCode 400 >=> text "text is required") next ctx
    else
        let! created = insertTodo db userId payload.text
        return! (setStatusCode 201
                 >=> setHttpHeader "Location" (sprintf "/api/todos/%d" created.id)
                 >=> json created) next ctx
}

let handlePatchTodo (id:int) : HttpHandler = fun next ctx -> task {
    let db = ctx.GetService<AppDbContext>()
    let userId = getUserId ctx
    let! patch = ctx.BindJsonAsync<UpdateTodo>()
    let! updated = updateTodo db userId id patch
    match updated with
    | Some t -> return! json t next ctx
    | None   -> return! (setStatusCode 404 >=> text "Not found") next ctx
}

let handleDeleteTodo (id:int) : HttpHandler = fun next ctx -> task {
    let db = ctx.GetService<AppDbContext>()
    let userId = getUserId ctx
    let! ok = deleteTodo db userId id
    if ok then return! (setStatusCode 204 >=> text "") next ctx
    else     return! (setStatusCode 404 >=> text "Not found") next ctx
}

let handleToggleTodo (id:int) : HttpHandler = fun next ctx -> task {
    let db = ctx.GetService<AppDbContext>()
    let userId = getUserId ctx
    let! res = toggleTodo db userId id
    match res with
    | Some t -> return! json t next ctx
    | None   -> return! (setStatusCode 404 >=> text "Not found") next ctx
}

let handleClearCompleted : HttpHandler = fun next ctx -> task {
    let db = ctx.GetService<AppDbContext>()
    let userId = getUserId ctx
    do! clearCompleted db userId
    return! (setStatusCode 204 >=> text "") next ctx
}

let handleRegister : HttpHandler = fun next ctx -> task {
    let db = ctx.GetService<AppDbContext>()
    let! req = ctx.BindJsonAsync<RegisterUserRequest>()

    if req = null || String.IsNullOrWhiteSpace(req.email) || String.IsNullOrWhiteSpace(req.password) then
        return! (setStatusCode 400 >=> text "Email and password are required.") next ctx
    else
        let! existingUser = findUserByEmail db req.email
        if existingUser <> null then
            return! (setStatusCode 409 >=> text "A user with that email already exists.") next ctx
        else
            let passwordHash = BCrypt.HashPassword(req.password)
            let newUser = { id = 0; email = req.email; passwordHash = passwordHash }
            db.Users.Add(newUser) |> ignore
            let! _ = db.SaveChangesAsync()
            
            // Sign in the user after successful registration
            let claims =
                [ Claim(ClaimTypes.Name, newUser.email)
                  Claim("UserId", newUser.id.ToString()) ]
            let identity = ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
            let principal = ClaimsPrincipal(identity)
            do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal)
            
            return! (setStatusCode 201 >=> json newUser) next ctx
}

let handleLogin : HttpHandler = fun next ctx -> task {
    let db = ctx.GetService<AppDbContext>()
    let! req = ctx.BindJsonAsync<LoginUserRequest>()

    if req = null || String.IsNullOrWhiteSpace(req.email) || String.IsNullOrWhiteSpace(req.password) then
        return! (setStatusCode 401 >=> text "Invalid credentials.") next ctx
    else
        let! user = findUserByEmail db req.email
        match Option.ofObj user with
        | None ->
            return! (setStatusCode 401 >=> text "Invalid credentials.") next ctx
        | Some user ->
            if BCrypt.Verify(req.password, user.passwordHash) then
                let claims =
                    [ Claim(ClaimTypes.Name, user.email)
                      Claim("UserId", user.id.ToString()) ]
                let identity = ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
                let principal = ClaimsPrincipal(identity)
                do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal)
                return! json user next ctx
            else
                return! (setStatusCode 401 >=> text "Invalid credentials.") next ctx
}

let handleLogout : HttpHandler = fun next ctx -> task {
    do! ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
    return! (setStatusCode 204) next ctx
}

// =====================
// Routes
// =====================

let webApp : HttpHandler =
    choose [
        route  "/" >=> text "OK"
        subRoute "/api/auth" (
            choose [
                POST >=> route "/register" >=> handleRegister
                POST >=> route "/login"    >=> handleLogin
                POST >=> route "/logout"   >=> handleLogout
            ]
        )
        subRoute "/api/todos" (
            requiresAuthentication >=> choose [
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

    // Manually load the migrations assembly in dev/runtime containers.
    // Context: EF Core needs to load 'Backend.DbMigrations' at runtime to discover migrations.
    // In some container publish layouts, the DLL isn't included in the .deps.json binding context,
    // so we proactively load it here if the file exists. This is a pragmatic fix for dev/docker-compose.
    // Production best practice: run migrations via a separate Kubernetes Job; then this load
    // is not required because the app won't need to discover migrations at startup.
    let migAssemblyName = "Backend.DbMigrations"
    let migAssemblyPath = Path.Combine(AppContext.BaseDirectory, migAssemblyName + ".dll")
    if File.Exists migAssemblyPath then
        try AssemblyLoadContext.Default.LoadFromAssemblyPath(migAssemblyPath) |> ignore with _ -> ()

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
            // Point EF Core to the assembly where our migrations are located.
            npgsqlOptions.MigrationsAssembly("Backend.DbMigrations") |> ignore
        ) |> ignore
    ) |> ignore

    // Add cookie authentication
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(fun options ->
            options.Cookie.Name <- "todoapp.auth"
            options.Cookie.HttpOnly <- true
            options.Cookie.SecurePolicy <- CookieSecurePolicy.SameAsRequest
            options.Cookie.SameSite <- SameSiteMode.Lax

            // On 401, don't redirect, just return the status code
            options.Events.OnRedirectToLogin <- (fun context ->
                context.Response.StatusCode <- 401
                Task.CompletedTask)
        ) |> ignore

    // Add CORS to allow frontend requests
    builder.Services.AddCors(fun options ->
        options.AddDefaultPolicy(fun policy ->
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials() |> ignore
        ) |> ignore
    ) |> ignore

    builder.Services.AddGiraffe() |> ignore

    let app = builder.Build()

    // In a real app, you might use a scope to get the DbContext
    // For this simple startup task, getting it directly is fine.
    use scope = app.Services.CreateScope()
    let dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>()
    let logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>()
    
    try
        // This applies any pending migrations to the database.
        // It's convenient for development but not recommended for production.
        dbContext.Database.Migrate()
        logger.LogInformation("Database migrations applied successfully")
        printfn "Database migrations applied successfully"
            
        // Test the connection by checking if we can connect to the database
        let canConnect = dbContext.Database.CanConnect()
        if canConnect then
            logger.LogInformation("Database connection test successful")
            printfn "Database connection test successful"
        else
            logger.LogWarning("Database connection test failed")
            printfn "Database connection test failed"
    with
    | ex -> 
        logger.LogError(ex, "Failed to initialize database")
        printfn "Failed to initialize database: %s" ex.Message
        reraise()

    // Add the /metrics endpoint for Prometheus
    app.UseOpenTelemetryPrometheusScrapingEndpoint() |> ignore

    // Enable CORS
    app.UseCors() |> ignore

    app.UseAuthentication() |> ignore

    if app.Environment.IsDevelopment() then
        // Seed development-specific data on startup.
        use scope = app.Services.CreateScope()
        let dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>()
        seedDevelopmentData dbContext |> Async.AwaitTask |> Async.RunSynchronously
    
    app.UseGiraffe webApp |> ignore
    app.Run()
    0