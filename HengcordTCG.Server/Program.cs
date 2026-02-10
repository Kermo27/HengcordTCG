using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Services;
using Scalar.AspNetCore;
using HengcordTCG.Server.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Set content root path to project directory for proper configuration loading
var projectRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..");
builder.Environment.ContentRootPath = Path.GetFullPath(projectRoot);

// Load configuration from appsettings and environment variables
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "HENGCORD_")
    .Build();

// Ensure data directory exists for SQLite database
var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDirectory);

// Add CORS for Web project on port 5000
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb", policy =>
    {
        policy.WithOrigins("http://localhost:5000", "https://localhost:5001")
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

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
var dbPath = Path.Combine(builder.Environment.ContentRootPath, connectionString.Replace("Data Source=", ""));
var absoluteConnectionString = $"Data Source={dbPath}";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        absoluteConnectionString,
        b => b.MigrationsAssembly("HengcordTCG.Server")));

// Business Services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TradeService>();
builder.Services.AddScoped<ShopService>();
builder.Services.AddScoped<CardImageService>(sp => 
{
    var contentRoot = builder.Environment.ContentRootPath;
    var assetPath = Path.Combine(contentRoot, "..", "Assets");
    return new CardImageService(assetPath);
});

var app = builder.Build();

// Apply database migrations automatically
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Applying database migrations...");
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

app.UseHttpsRedirection();

app.UseMiddleware<RateLimitMiddleware>();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseMiddleware<ApiKeyAuthMiddleware>();

app.UseCors("AllowWeb");

app.UseAuthorization();

app.MapControllers();

app.Run();
