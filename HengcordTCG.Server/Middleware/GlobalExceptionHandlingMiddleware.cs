using System.Net;
using System.Text.Json;

namespace HengcordTCG.Server.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var logger = context.RequestServices.GetRequiredService<ILogger<GlobalExceptionHandlingMiddleware>>();

        var response = new ErrorResponse
        {
            Message = exception.Message,
            Timestamp = DateTime.UtcNow
        };

        switch (exception)
        {
            case ArgumentException argEx:
                logger.LogWarning(argEx, "Argument validation error: {Message}", argEx.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Message = $"Validation Error: {argEx.Message}";
                break;

            case DbUpdateException dbEx:
                logger.LogError(dbEx, "Database update error occurred");
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Message = "Database operation failed. Please try again later.";
                break;

            case KeyNotFoundException notFoundEx:
                logger.LogWarning(notFoundEx, "Resource not found");
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Message = notFoundEx.Message;
                break;

            default:
                logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Message = "An unexpected error occurred. Please try again later.";
                response.TraceId = context.TraceIdentifier;
                break;
        }

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(response, jsonOptions);
        return context.Response.WriteAsync(json);
    }
}

public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? TraceId { get; set; }
}
