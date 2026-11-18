using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Api.Infrastructure;

// Last-resort handler: logs the exception (structured, with the trace id) and returns a generic
// ProblemDetails. Exception messages and stack traces never reach the client.
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            // e.g. a unique index hit by two simultaneous requests
            DbUpdateException => (StatusCodes.Status409Conflict, "The change conflicts with existing data",
                "The data was changed by another request or violates a uniqueness rule. Reload and try again."),
            BadHttpRequestException bad => (bad.StatusCode, "Bad request", "The request could not be read."),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => (499, "Request cancelled", "The client closed the request."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred",
                "The server could not complete the request. The error has been logged."),
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "Request failed with {StatusCode} for {Method} {Path}", status, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}
