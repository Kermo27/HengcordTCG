using HengcordTCG.Blazor;
using HengcordTCG.Blazor.Client.Services;
using HengcordTCG.Shared.Clients;
using Microsoft.AspNetCore.Components.Authorization;
using BlazorBlueprint.Primitives.Extensions;
using BlazorBlueprint.Components.Toast;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddBlazorBlueprintPrimitives();
builder.Services.AddScoped<ToastService>();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7156";
var apiKey = builder.Configuration["ApiKey"] ?? "dev-key";

builder.Services.AddScoped(sp =>
{
    var handler = new HttpClientHandler
    {
        UseCookies = true,
        CookieContainer = new System.Net.CookieContainer()
    };
    
    var http = new HttpClient(handler)
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
    http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    return http;
});

builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<HengcordTCG.Shared.Clients.HengcordTCGClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(HengcordTCG.Blazor.Client._Imports).Assembly);

app.Run();
