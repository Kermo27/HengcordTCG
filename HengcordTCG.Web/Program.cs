using HengcordTCG.Web;
using BlazorBlueprint.Primitives.Extensions;
using BlazorBlueprint.Components.Toast;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using HengcordTCG.Shared.Clients;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddCascadingAuthenticationState();

// Add BlazorBlueprint services
builder.Services.AddBlazorBlueprintPrimitives();
builder.Services.AddScoped<ToastService>();

// API Client for syncing users with API Key authentication
builder.Services.AddHttpClient<HengcordTCGClient>(client =>
{
    var serverUrl = builder.Configuration["ServerUrl"] ?? "http://localhost:5266";
    var webApiKey = builder.Configuration["ApiKey"] ?? "YOUR_WEB_API_KEY";
    client.BaseAddress = new Uri(serverUrl);
    client.DefaultRequestHeaders.Add("X-API-Key", webApiKey);
});

// Authentication
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "Discord";
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "HengcordTCG.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest; // Allow HTTP
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    })
    .AddDiscord(options =>
    {
        options.ClientId = builder.Configuration["Discord:ClientId"] ?? "PLACEHOLDER";
        options.ClientSecret = builder.Configuration["Discord:ClientSecret"] ?? "PLACEHOLDER";
        options.SaveTokens = true;
        
        // Scope for getting user info
        options.Scope.Add("identify");
        options.Scope.Add("email");

        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
        options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

        options.Events.OnCreatingTicket = async context =>
        {
            var sp = context.HttpContext.RequestServices;
            var client = sp.GetRequiredService<HengcordTCGClient>();
            
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

                try
                {
                    var userModel = await client.GetOrCreateUserAsync(discordId, username);
                    if (userModel?.IsBotAdmin == true)
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

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Auth info for Blazor WebAssembly client
app.MapGet("/auth/me", (ClaimsPrincipal user) =>
{
    var isAuthenticated = user.Identity?.IsAuthenticated ?? false;
    var name = user.Identity?.Name;
    var avatarUrl = user.FindFirst("avatar_url")?.Value;
    var isAdmin = user.HasClaim("is_admin", "true");

    return Results.Ok(new
    {
        isAuthenticated,
        name,
        avatarUrl,
        isAdmin
    });
}).RequireAuthorization();

// Auth Endpoints
app.MapGet("/login", async (HttpContext context) =>
{
    await context.ChallengeAsync("Discord", new AuthenticationProperties { RedirectUri = "/" });
});

app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
    context.Response.Redirect("/");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(HengcordTCG.Web.Client._Imports).Assembly);

app.Run();
