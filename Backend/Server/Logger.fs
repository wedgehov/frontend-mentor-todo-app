module Logger

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

let configure (builder: WebApplicationBuilder) : unit =
    // If compiled with SERILOG symbol and packages are available, use Serilog.
    // Otherwise, fall back to built-in console/json logging to keep compile happy.
#if SERILOG
    Serilog.Log.Logger <-
        Serilog
            .LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.Console()
            .CreateLogger()
    builder.Host.UseSerilog() |> ignore
#else
    builder.Logging.ClearProviders() |> ignore
    if builder.Environment.IsDevelopment() then
        builder.Logging.AddConsole() |> ignore
    else
        builder.Logging.AddJsonConsole() |> ignore
#endif
