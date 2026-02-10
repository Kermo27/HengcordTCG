using System.Collections.Concurrent;
using System.Net;

namespace HengcordTCG.Server.Middleware;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _requestCounts = new();
    private readonly int _requestsPerMinute = 60;
    private readonly int _requestsPerHour = 1000;

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var identifier = GetClientIdentifier(context);
        var now = DateTime.UtcNow;

        if (!_requestCounts.TryGetValue(identifier, out var entry))
        {
            entry = new RateLimitEntry();
            _requestCounts.TryAdd(identifier, entry);
        }

        // Clean up old entries older than 1 hour
        if ((now - entry.LastResetHour).TotalMinutes > 60)
        {
            entry.HourlyCount = 0;
            entry.LastResetHour = now;
        }

        // Clean up old entries older than 1 minute
        if ((now - entry.LastResetMinute).TotalSeconds > 60)
        {
            entry.MinuteCount = 0;
            entry.LastResetMinute = now;
        }

        // Check hourly limit
        if (entry.HourlyCount >= _requestsPerHour)
        {
            _logger.LogWarning("Rate limit exceeded (hourly) for client {Identifier}", identifier);
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            await context.Response.WriteAsync("Rate limit exceeded. Try again later.");
            return;
        }

        // Check minute limit
        if (entry.MinuteCount >= _requestsPerMinute)
        {
            _logger.LogWarning("Rate limit exceeded (per minute) for client {Identifier}", identifier);
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            await context.Response.WriteAsync("Rate limit exceeded. Try again in a minute.");
            return;
        }

        entry.MinuteCount++;
        entry.HourlyCount++;

        await _next(context);
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // Try to use API key as identifier, fallback to IP address
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
