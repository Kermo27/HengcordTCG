using HengcordTCG.Web;
using BlazorBlueprint.Primitives.Extensions;
using BlazorBlueprint.Components.Toast;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Add BlazorBlueprint services
builder.Services.AddBlazorBlueprintPrimitives();
builder.Services.AddScoped<ToastService>();

var app = builder.Build();

// Configure the HTTP request pipeline
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
    .AddAdditionalAssemblies(typeof(HengcordTCG.Web.Client._Imports).Assembly);

app.Run();
