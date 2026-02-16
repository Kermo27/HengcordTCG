using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using HengcordTCG.Server.Authentication;

namespace HengcordTCG.Server.Middleware;

public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;
    private readonly string[] _publicPaths = { "/scalar", "/openapi", "/health", "/api/auth", "/api/users/stats", "/api/users/top-gold", "/api/cards" };

    public ApiKeyAuthMiddleware(RequestDelegate next, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        var path = context.Request.Path.Value ?? "";

        if (_publicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null)
        {
            await _next(context);
            return;
        }

        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Check if user is already authenticated (by JWT or other schemes)
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            _logger.LogInformation("User {User} authenticated for {Method} {Path}", 
                context.User.Identity.Name, context.Request.Method, path);
            await _next(context);
            return;
        }

        // Check for API key
        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            var validApiKeys = configuration.GetSection("ApiKeys")?.Get<string[]>() ?? Array.Empty<string>();
            var providedKey = apiKeyHeader.ToString();

            if (validApiKeys.Length > 0 && validApiKeys.Contains(providedKey))
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "ApiKey"),
                    new Claim(ClaimTypes.Name, "Bot")
                };
                var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.DefaultScheme);
                var principal = new ClaimsPrincipal(identity);
                context.User = principal;

                _logger.LogInformation("API key authenticated for {Method} {Path}", context.Request.Method, path);
                await _next(context);
                return;
            }
        }

        // Let the request pass through - JWT authentication will handle it in the authorization pipeline
        await _next(context);
    }
}
