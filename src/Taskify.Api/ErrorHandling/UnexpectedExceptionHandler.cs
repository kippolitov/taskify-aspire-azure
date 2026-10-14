using Microsoft.AspNetCore.Diagnostics;
using Taskify.Api.Observability;

namespace Taskify.Api.ErrorHandling;

/// <summary>
/// Logs an unexpected exception and answers with a 500 Problem Details body that carries
/// no exception text or stack trace (FR-012).
/// </summary>
public sealed class UnexpectedExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<UnexpectedExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        Log.UnexpectedError(logger, exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails =
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                },
            }
        );
    }
}
