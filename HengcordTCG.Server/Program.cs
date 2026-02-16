using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Services;
using Scalar.AspNetCore;
using HengcordTCG.Server.Middleware;
using HengcordTCG.Server.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using System.Text;
using HengcordTCG.Server.Services;
using Microsoft.IdentityModel.Tokens;

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
builder.Services.AddScoped<WikiService>();
builder.Services.AddScoped<WikiProposalService>();

// Authentication & Discord OAuth
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/api/auth/login";
    options.LogoutPath = "/api/auth/logout";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "HengcordTCG",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "HengcordTCG.Web",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.NameIdentifier
    };
    
    // Handle authentication failures gracefully
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            // Log the error but don't throw
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(context.Exception, "JWT authentication failed");
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            // Return 401 instead of 500 when token is missing/invalid
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync("{\"message\":\"Authentication required\"}");
        }
    };
})
.AddDiscord(options =>
{
    options.ClientId = builder.Configuration["Discord:ClientId"]!;
    options.ClientSecret = builder.Configuration["Discord:ClientSecret"]!;
    options.CallbackPath = "/signin-discord";
    options.Scope.Add("identify");
    options.SaveTokens = true;
    
    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
    options.ClaimActions.MapJsonKey("urn:discord:avatar", "avatar");
    options.ClaimActions.MapJsonKey("urn:discord:discriminator", "discriminator");
    
    options.Events.OnCreatingTicket = async context =>
    {
        // Ensure user is synced to database
        var discordId = context.Identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = context.Identity?.FindFirst(ClaimTypes.Name)?.Value;
        
        if (!string.IsNullOrEmpty(discordId) && !string.IsNullOrEmpty(username))
        {
            // This will be handled by the next request to sync the user
        }
    };
})
.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.DefaultScheme, null);

builder.Services.AddAuthorization();

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

app.UseCors("AllowWeb");

// Authentication must come BEFORE ApiKeyAuthMiddleware so User context is populated
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapControllers();

app.Run();
