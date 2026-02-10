using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Services;
using Scalar.AspNetCore;
using HengcordTCG.Server.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Json;

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
    ?? new[] { "http://localhost:5000", "https://localhost:5001" };

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

// Auth (Discord OAuth -> Cookie) for the web client (Blazor WASM)
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "Discord";
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "HengcordTCG.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.None; // cross-site (separate WASM origin)
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // required for SameSite=None in modern browsers

        // For API calls from SPA: don't redirect, return proper status codes
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        };
    })
    .AddDiscord(options =>
    {
        options.ClientId = builder.Configuration["Discord:ClientId"] ?? "PLACEHOLDER";
        options.ClientSecret = builder.Configuration["Discord:ClientSecret"] ?? "PLACEHOLDER";
        options.SaveTokens = true;

        options.Scope.Add("identify");
        options.Scope.Add("email");

        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
        options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

        options.Events.OnCreatingTicket = async context =>
        {
            var sp = context.HttpContext.RequestServices;
            var userService = sp.GetRequiredService<UserService>();

            var discordIdStr = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!ulong.TryParse(discordIdStr, out var discordId))
            {
                return;
            }

            var username = context.Principal?.FindFirst(ClaimTypes.Name)?.Value
                           ?? context.Principal?.Identity?.Name
                           ?? $"User_{discordId}";

            // Build Discord avatar URL if available
            string? avatarUrl = null;
            try
            {
                if (context.User.TryGetProperty("avatar", out JsonElement avatarElement) &&
                    avatarElement.ValueKind == JsonValueKind.String)
                {
                    var avatarHash = avatarElement.GetString();
                    if (!string.IsNullOrEmpty(avatarHash))
                    {
                        avatarUrl = $"https://cdn.discordapp.com/avatars/{discordId}/{avatarHash}.png?size=64";
                    }
                }
            }
            catch
            {
                // ignore avatar failures
            }

            if (context.Principal?.Identity is ClaimsIdentity identity)
            {
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    identity.AddClaim(new Claim("avatar_url", avatarUrl));
                }

                // Ensure user exists + set admin claim from DB
                try
                {
                    var userModel = await userService.GetOrCreateUserAsync(discordId, username);
                    if (userModel.IsBotAdmin)
                    {
                        identity.AddClaim(new Claim("is_admin", "true"));
                    }
                }
                catch
                {
                    // Ignore failures when syncing user data during login
                }
            }
        };
    });

builder.Services.AddAuthorization();

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

// CardImageService - use configurable assets path
var assetsPath = builder.Configuration["AssetsPath"] 
    ?? Path.Combine(builder.Environment.ContentRootPath, "Assets");
builder.Services.AddScoped<CardImageService>(sp => new CardImageService(assetsPath));

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

app.UseHttpsRedirection();

app.UseCors("AllowWeb");

app.UseMiddleware<RateLimitMiddleware>();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
