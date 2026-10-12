using Serilog;
using Serilog.Events;
using System.Text.Json.Serialization;
using RogueNet.Logic.MapGeneration;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Error)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    Log.Information("Starting RogueNet API in {EnvironmentName}", builder.Environment.EnvironmentName);

    builder.Host.UseSerilog((_, loggerConfiguration) => loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Error));

    // Add services to the container.
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();
    builder.Services.AddSingleton<DungeonMapGenerator>();

    var clientOrigin = builder.Configuration["ClientOrigin"] ?? "http://localhost:5173";
    builder.Services.AddCors(options => options.AddPolicy("LocalClient", policy =>
        policy.WithOrigins(clientOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()));
    Log.Information("Configured browser CORS for {ClientOrigin}", clientOrigin);

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    if (builder.Configuration.GetValue("EnableHttpsRedirection", false))
        app.UseHttpsRedirection();

    app.UseCors("LocalClient");
    app.UseAuthorization();
    app.MapHealthChecks("/health");
    app.MapControllers();

    app.Lifetime.ApplicationStarted.Register(() =>
        Log.Information("RogueNet API started"));
    app.Lifetime.ApplicationStopping.Register(() =>
        Log.Information("RogueNet API is stopping"));

    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "RogueNet API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
