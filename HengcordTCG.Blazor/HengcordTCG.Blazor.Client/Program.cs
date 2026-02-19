using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using HengcordTCG.Shared.Clients;
using HengcordTCG.Blazor.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7156";

builder.Services.AddScoped(sp =>
{
    var http = new HttpClient(new IncludeCredentialsHandler())
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
    return http;
});

builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy(AuthPolicy.Admin, policy => policy.RequireRole("Admin"));
    options.AddPolicy(AuthPolicy.User, policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddScoped<HengcordTCGClient>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<WikiService>();

await builder.Build().RunAsync();

internal class IncludeCredentialsHandler : DelegatingHandler
{
    public IncludeCredentialsHandler() : base(new HttpClientHandler())
    {
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, cancellationToken);
    }
}
