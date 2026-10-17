using System.Diagnostics;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.API.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        await next(context);
        stopwatch.Stop();

        var statusCode = context.Response.StatusCode;
        var logLevel = statusCode >= StatusCodes.Status500InternalServerError
            ? LogLevel.Error
            : statusCode >= StatusCodes.Status400BadRequest
                ? LogLevel.Warning
                : LogLevel.Information;

        logger.Log(
            logLevel,
            "HTTP request completed {method} {rota} with status {status} in {duracao} ms (trace {traceId})",
            context.Request.Method,
            context.Request.Path.Value ?? "/",
            statusCode,
            Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
            Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier);
    }
}
