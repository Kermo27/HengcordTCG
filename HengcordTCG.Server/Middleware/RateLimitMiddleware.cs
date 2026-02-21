using System.Collections.Concurrent;
using System.Net;

namespace HengcordTCG.Server.Middleware;

public class RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<RateLimitMiddleware> _logger = logger;
    private static readonly ConcurrentDictionary<string, RateLimitEntry> RequestCounts = new();
    private static int _requestCounter;
    private const int RequestsPerMinute = 120;
    private const int RequestsPerHour = 5000;

    public async Task InvokeAsync(HttpContext context)
    {
        var identifier = GetClientIdentifier(context);
        var now = DateTime.UtcNow;

        // Periodic cleanup of stale entries to prevent memory leak
        if (Interlocked.Increment(ref _requestCounter) % 1000 == 0)
        {
            var cutoff = now.AddHours(-2);
            var staleKeys = RequestCounts
                .Where(kvp => kvp.Value.LastResetHour < cutoff)
                .Select(kvp => kvp.Key).ToList();
            foreach (var key in staleKeys)
                RequestCounts.TryRemove(key, out _);
        }

        var entry = RequestCounts.GetOrAdd(identifier, _ => new RateLimitEntry());

        bool shouldReturn;
        string? returnMessage;

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
                returnMessage = "Rate limit exceeded. Try again later.";
                shouldReturn = true;
            }
            else if (entry.MinuteCount >= RequestsPerMinute)
            {
                _logger.LogWarning("Rate limit exceeded (per minute) for client {Identifier}", identifier);
                returnMessage = "Rate limit exceeded. Try again in a minute.";
                shouldReturn = true;
            }
            else
            {
                entry.MinuteCount++;
                entry.HourlyCount++;
                shouldReturn = false;
                returnMessage = null;
            }
        }

        if (shouldReturn)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($"{{\"message\":\"{returnMessage}\"}}");
            return;
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
