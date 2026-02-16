using System.Collections.Concurrent;
using System.Net;

namespace HengcordTCG.Server.Middleware;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _requestCounts = new();
    private const int RequestsPerMinute = 120;
    private const int RequestsPerHour = 5000;

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var identifier = GetClientIdentifier(context);
        var now = DateTime.UtcNow;

        var entry = _requestCounts.GetOrAdd(identifier, _ => new RateLimitEntry());

        lock (entry)
        {
            if ((now - entry.LastResetHour).TotalMinutes > 60)
            {
                entry.HourlyCount = 0;
                entry.LastResetHour = now;
            }

            if ((now - entry.LastResetMinute).TotalSeconds > 60)
            {
                entry.MinuteCount = 0;
                entry.LastResetMinute = now;
            }

            if (entry.HourlyCount >= RequestsPerHour)
            {
                _logger.LogWarning("Rate limit exceeded (hourly) for client {Identifier}", identifier);
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"message\":\"Rate limit exceeded. Try again later.\"}");
                return;
            }

            if (entry.MinuteCount >= RequestsPerMinute)
            {
                _logger.LogWarning("Rate limit exceeded (per minute) for client {Identifier}", identifier);
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"message\":\"Rate limit exceeded. Try again in a minute.\"}");
                return;
            }

            entry.MinuteCount++;
            entry.HourlyCount++;
        }

        await _next(context);
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKey))
        {
            return $"api-key:{apiKey}";
        }

        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            return $"ip:{forwardedFor.ToString().Split(',')[0].Trim()}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}

public class RateLimitEntry
{
    public int MinuteCount { get; set; }
    public int HourlyCount { get; set; }
    public DateTime LastResetMinute { get; set; } = DateTime.UtcNow;
    public DateTime LastResetHour { get; set; } = DateTime.UtcNow;
}
