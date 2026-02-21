using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Services;
using Scalar.AspNetCore;
using HengcordTCG.Server.Middleware;
using HengcordTCG.Server.Authentication;
using HengcordTCG.Server.Validators;
using HengcordTCG.Server.Services;
using HengcordTCG.Server.Mapping;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using Serilog;
using Microsoft.AspNetCore.HttpOverrides;

var isTesting = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Testing";

if (!isTesting)
{
    try
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .Build())
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }
    catch
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }

    Log.Information("Starting HengcordTCG Server");
}

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Re-check using builder environment (WebApplicationFactory sets this via UseEnvironment)
    isTesting = isTesting || builder.Environment.EnvironmentName == "Testing";

    if (!isTesting)
    {
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));
    }

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables(prefix: "HENGCORD_")
        .Build();

    var dataDirectory = Environment.GetEnvironmentVariable("HENGCORD_DATA_DIR") 
        ?? Path.Combine(builder.Environment.ContentRootPath, "data");
    Directory.CreateDirectory(dataDirectory);

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

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });
    builder.Services.AddValidatorsFromAssemblyContaining<CardValidator>();
    builder.Services.AddAutoMapper(typeof(MappingProfile));
    builder.Services.AddOpenApi();

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=app.db";
    string dbPath;
    if (Path.IsPathRooted(connectionString.Replace("Data Source=", "")))
    {
        dbPath = connectionString.Replace("Data Source=", "");
    }
    else
    {
        var relativePath = connectionString.Replace("Data Source=", "").TrimStart('.', '/', '\\');
        if (relativePath.StartsWith("data/") || relativePath.StartsWith("data\\"))
        {
            relativePath = relativePath.Substring(5);
        }
        dbPath = Path.Combine(dataDirectory, relativePath);
    }
    var absoluteConnectionString = $"Data Source={dbPath}";

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(
            absoluteConnectionString,
            b => b.MigrationsAssembly("HengcordTCG.Shared")));

    builder.Services.AddScoped<UserService>();
    builder.Services.AddScoped<TradeService>();
    builder.Services.AddScoped<ShopService>();
    builder.Services.AddScoped<WikiService>();
    builder.Services.AddScoped<WikiProposalService>();

    builder.Services.AddScoped<ICardService, CardService>();
    builder.Services.AddScoped<IPackService, PackService>();
    builder.Services.AddScoped<IDeckService, DeckService>();
    builder.Services.AddScoped<IMatchService, MatchService>();

    var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "DEFAULT_DEV_SECRET_CHANGE_ME_123456789";

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
            NameClaimType = ClaimTypes.Name
        };
        
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token))
                {
                    context.Token = context.Request.Cookies["auth_token"];
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Log.Warning(context.Exception, "JWT authentication failed");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"message\":\"Authentication required\"}");
            }
        };
    })
    .AddDiscord(options =>
    {
        options.ClientId = builder.Configuration["Discord:ClientId"] ?? "placeholder";
        options.ClientSecret = builder.Configuration["Discord:ClientSecret"] ?? "placeholder";
        options.CallbackPath = "/signin-discord";
        options.Scope.Add("identify");
        options.SaveTokens = true;
        
        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey("urn:discord:username", "username");
        options.ClaimActions.MapJsonKey("urn:discord:global_name", "global_name");
        options.ClaimActions.MapJsonKey("urn:discord:avatar", "avatar");
        options.ClaimActions.MapJsonKey("urn:discord:discriminator", "discriminator");
        
        options.Events.OnCreatingTicket = async context =>
        {
            var discordId = context.Identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var accessToken = context.Properties?.Items.FirstOrDefault(x => x.Key == ".Token.access_token").Value;
            
            if (!string.IsNullOrEmpty(discordId) && !string.IsNullOrEmpty(accessToken))
            {
                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    var response = await httpClient.GetAsync("https://discord.com/api/users/@me");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
                        var root = json.RootElement;
                        
                        if (root.TryGetProperty("global_name", out var globalName) && globalName.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            context.Identity?.AddClaim(new Claim("urn:discord:global_name", globalName.GetString()!));
                        }
                        if (root.TryGetProperty("username", out var username) && username.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            context.Identity?.AddClaim(new Claim("urn:discord:username", username.GetString()!));
                        }
                        if (root.TryGetProperty("avatar", out var avatar) && avatar.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            context.Identity?.AddClaim(new Claim("urn:discord:avatar", avatar.GetString()!));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to fetch additional Discord user info");
                }
            }
        };
    })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.DefaultScheme, null);

    builder.Services.AddAuthorization();

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!isTesting)
            {
                Log.Information("Applying database migrations...");
                Log.Information("Database path: {DbPath}", dbPath);
            }
            if (db.Database.IsRelational())
            {
                await db.Database.MigrateAsync();
                if (!isTesting)
                {
                    Log.Information("Database migrations applied successfully");
                }
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
                if (!isTesting)
                {
                    Log.Information("In-memory database created successfully");
                }
            }
        }
        catch (Exception ex)
        {
            if (!isTesting)
            {
                Log.Fatal(ex, "Error applying database migrations");
            }
            throw;
        }
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    if (!isTesting)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            };
        });
    }

    app.UseForwardedHeaders();

    app.UseMiddleware<RateLimitMiddleware>();
    app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    app.UseCors("AllowWeb");
    app.UseAuthentication();
    app.UseAuthorization();
    // ApiKeyAuthenticationHandler handles API key auth via ASP.NET auth pipeline
    
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
    
    app.MapControllers();

    if (!isTesting)
    {
        Log.Information("HengcordTCG Server started successfully");
    }

    app.Run();
}
catch (Exception ex)
{
    if (!isTesting)
    {
        Log.Fatal(ex, "Application terminated unexpectedly");
    }
    throw;
}
finally
{
    if (!isTesting)
    {
        Log.CloseAndFlush();
    }
}

public partial class Program { }
