using System.Net;
using Course.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Course.Services.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail) = exception switch
        {
            AppException appException =>
                ((int)appException.StatusCode, GetTitle(appException.StatusCode), appException.Message),
            _ =>
                ((int)HttpStatusCode.InternalServerError, "Unexpected error", "An unexpected error occurred.")
        };

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled exception while processing request {Path}", context.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Handled application exception for request {Path}", context.Request.Path);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        if (_environment.IsDevelopment())
        {
            problem.Extensions["traceId"] = context.TraceIdentifier;
            problem.Extensions["exception"] = exception.GetType().Name;
        }

        await context.Response.WriteAsJsonAsync(problem);
    }

    private static string GetTitle(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "Validation failed",
            HttpStatusCode.Unauthorized => "Authentication required",
            HttpStatusCode.Forbidden => "Forbidden",
            HttpStatusCode.NotFound => "Resource not found",
            _ => "Error"
        };
    }
}
