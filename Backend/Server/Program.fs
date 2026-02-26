module Backend.Program

open System
open System.IO
open System.Runtime.Loader
open System.Threading.Tasks
open Data
open Entity
open Giraffe
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Npgsql

type AppDbContext = Entity.AppDbContext

let webApp: HttpHandler =
    choose [
        route "/" >=> text "OK"
        subRoute
            "/api/auth"
            (choose [
                POST >=> route "/register" >=> global.Auth.handleRegister
                POST >=> route "/login" >=> global.Auth.handleLogin
                POST >=> route "/logout" >=> global.Auth.handleLogout
            ])
        subRoute
            "/api/todos"
            (global.Auth.requiresAuthentication
             >=> choose [
                 GET >=> route "" >=> global.Todos.handleGetTodos
                 POST >=> route "" >=> global.Todos.handleCreateTodo
                 PUT >=> routef "/%i/toggle" global.Todos.handleToggleTodo
                 DELETE >=> routef "/%i" global.Todos.handleDeleteTodo
                 DELETE >=> route "/completed" >=> global.Todos.handleClearCompleted
                 PATCH >=> routef "/%i" global.Todos.handlePatchTodo
             ])
    ]

[<EntryPoint>]
let main argv =
    let builder = WebApplication.CreateBuilder(argv)

    let migAssemblyName = "Backend.Migrations"
    let migAssemblyPath =
        Path.Combine(AppContext.BaseDirectory, migAssemblyName + ".dll")

    if File.Exists migAssemblyPath then
        try
            AssemblyLoadContext.Default.LoadFromAssemblyPath(migAssemblyPath) |> ignore
        with _ ->
            ()

    Logger.configure builder
    Observability.addOpenTelemetry builder.Services

    builder.Services.AddSingleton<NpgsqlDataSource>(fun sp ->
        let cfg = sp.GetRequiredService<IConfiguration>()
        let connStr = cfg.GetConnectionString("DefaultConnection")
        let env = sp.GetRequiredService<IHostEnvironment>()
        let loggerFactory = sp.GetRequiredService<ILoggerFactory>()
        let dataSourceBuilder = NpgsqlDataSourceBuilder(connStr)
        dataSourceBuilder.UseLoggerFactory(loggerFactory) |> ignore

        if env.IsDevelopment() then
            dataSourceBuilder.EnableParameterLogging() |> ignore

        dataSourceBuilder.Build()
    )
    |> ignore

    builder.Services.AddDbContext<AppDbContext>(fun
                                                    (sp: IServiceProvider)
                                                    (options: DbContextOptionsBuilder) ->
        let dataSource = sp.GetRequiredService<NpgsqlDataSource>()

        options.UseNpgsql(
            dataSource,
            fun npgsqlOptions -> npgsqlOptions.MigrationsAssembly("Backend.Migrations") |> ignore
        )
        |> ignore
    )
    |> ignore

    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(fun options ->
            options.Cookie.Name <- "todoapp.auth"
            options.Cookie.HttpOnly <- true
            options.Cookie.SecurePolicy <- CookieSecurePolicy.SameAsRequest
            options.Cookie.SameSite <- SameSiteMode.Lax
            options.Events.OnRedirectToLogin <-
                (fun context ->
                    context.Response.StatusCode <- 401
                    Task.CompletedTask
                )
        )
    |> ignore

    builder.Services.AddCors(fun options ->
        options.AddDefaultPolicy(fun policy ->
            policy
                .WithOrigins("http://localhost:5173")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
            |> ignore
        )
        |> ignore
    )
    |> ignore

    builder.Services.AddGiraffe() |> ignore

    let app = builder.Build()
    use scope = app.Services.CreateScope()
    let dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>()
    let logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>()

    let maxRetries = 10
    let delay = TimeSpan.FromSeconds(5.0)
    let mutable retries = 0
    let mutable connected = false

    while not connected && retries < maxRetries do
        try
            dbContext.Database.Migrate()
            logger.LogInformation("Database migrations applied successfully")
            printfn "Database migrations applied successfully"
            connected <- true

            let canConnect = dbContext.Database.CanConnect()

            if canConnect then
                logger.LogInformation("Database connection test successful")
                printfn "Database connection test successful"
            else
                logger.LogWarning("Database connection test failed")
                printfn "Database connection test failed"
        with ex ->
            retries <- retries + 1
            logger.LogError(
                ex,
                "Failed to initialize database. Attempt {RetryCount}/{MaxRetries}",
                retries,
                maxRetries
            )
            printfn
                "Failed to initialize database. Attempt %d/%d. Retrying in %f seconds..."
                retries
                maxRetries
                delay.TotalSeconds

            if retries < maxRetries then
                Task.Delay(delay).Wait()

    if not connected then
        let errorMsg = "Could not connect to the database after multiple retries. Exiting."
        logger.LogCritical(errorMsg)
        printfn "%s" errorMsg
        Environment.Exit(1)

    Observability.addPrometheusEndpoint app
    app.UseCors() |> ignore
    app.UseAuthentication() |> ignore

    if app.Environment.IsDevelopment() then
        use scope = app.Services.CreateScope()
        let dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>()
        seedDevelopmentData dbContext |> Async.AwaitTask |> Async.RunSynchronously

    app.UseGiraffe webApp |> ignore
    app.Run()
    0
