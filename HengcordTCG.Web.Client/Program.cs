using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using HengcordTCG.Web.Client;
using BlazorBlueprint.Primitives.Extensions;
using BlazorBlueprint.Components.Toast;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddBlazorBlueprintPrimitives();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();

// HTTP client pointing to the same origin as the host app
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

await builder.Build().RunAsync();
