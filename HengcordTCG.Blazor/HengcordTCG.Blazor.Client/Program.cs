using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorBlueprint.Primitives.Extensions;
using BlazorBlueprint.Components.Toast;
using HengcordTCG.Shared.Clients;
using HengcordTCG.Blazor.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Configure HttpClient for API communication
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7156";

builder.Services.AddScoped(sp =>
{
    var http = new HttpClient
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
    return http;
});

// Register Auth State Provider
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy(AuthPolicy.Admin, policy => policy.RequireRole("Admin"));
    options.AddPolicy(AuthPolicy.User, policy => policy.RequireAuthenticatedUser());
});

// Register HengcordTCGClient for API access
builder.Services.AddScoped<HengcordTCGClient>();

// Register Auth Service
builder.Services.AddScoped<AuthService>();

// Register Wiki Service
builder.Services.AddScoped<WikiService>();

// BlazorBlueprint
builder.Services.AddBlazorBlueprintPrimitives();
builder.Services.AddScoped<ToastService>();

await builder.Build().RunAsync();
