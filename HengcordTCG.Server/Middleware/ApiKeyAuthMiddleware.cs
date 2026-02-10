using System.Net;

namespace HengcordTCG.Server.Middleware;

public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;
    private readonly string[] _publicPaths = { "/scalar", "/openapi", "/health" };

    public ApiKeyAuthMiddleware(RequestDelegate next, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip API key check for public endpoints and development docs
        if (_publicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Skip API key check for non-API routes
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // All /api/* routes require API key
        if (!context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            _logger.LogWarning("Missing X-API-Key header for {Method} {Path}", context.Request.Method, path);
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsync("Missing or invalid API key");
            return;
        }

        var validApiKeys = configuration.GetSection("ApiKeys")?.Get<string[]>() ?? Array.Empty<string>();

        if (validApiKeys.Length == 0)
        {
            _logger.LogError("No API keys configured!");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsync("Server configuration error");
            return;
        }

        if (!validApiKeys.Contains(apiKeyHeader.ToString()))
        {
            _logger.LogWarning("Invalid API-Key provided for {Method} {Path}", context.Request.Method, path);
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsync("Missing or invalid API key");
            return;
        }

        _logger.LogInformation("API key validated for {Method} {Path}", context.Request.Method, path);
        await _next(context);
    }
}
