using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Services;
using Scalar.AspNetCore;
using HengcordTCG.Server.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load configuration from appsettings and environment variables
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "HENGCORD_")
    .Build();

// Determine data directory - use environment variable or default to ./data
var dataDirectory = Environment.GetEnvironmentVariable("HENGCORD_DATA_DIR") 
    ?? Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDirectory);

// Add CORS - configurable via appsettings
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "https://localhost:5001" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddOpenApi();

// Database Configuration - support both relative and absolute paths
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
string dbPath;
if (Path.IsPathRooted(connectionString.Replace("Data Source=", "")))
{
    // Absolute path provided
    dbPath = connectionString.Replace("Data Source=", "");
}
else
{
    // Relative path - resolve from data directory
    var relativePath = connectionString.Replace("Data Source=", "").TrimStart('.', '/', '\\');
    if (relativePath.StartsWith("data/") || relativePath.StartsWith("data\\"))
    {
        relativePath = relativePath.Substring(5); // Remove "data/" prefix
    }
    dbPath = Path.Combine(dataDirectory, relativePath);
}
var absoluteConnectionString = $"Data Source={dbPath}";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        absoluteConnectionString,
        b => b.MigrationsAssembly("HengcordTCG.Shared")));

// Business Services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TradeService>();
builder.Services.AddScoped<ShopService>();

var app = builder.Build();

// Apply database migrations automatically
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Applying database migrations...");
        logger.LogInformation("Database path: {DbPath}", dbPath);
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error applying database migrations");
        throw;
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<RateLimitMiddleware>();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseMiddleware<ApiKeyAuthMiddleware>();

app.UseCors("AllowWeb");

app.UseAuthorization();

app.MapControllers();

app.Run();
