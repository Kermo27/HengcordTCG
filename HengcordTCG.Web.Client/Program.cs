using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorBlueprint.Primitives.Extensions;
using BlazorBlueprint.Components.Toast;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddBlazorBlueprintPrimitives();
builder.Services.AddScoped<ToastService>();

await builder.Build().RunAsync();
