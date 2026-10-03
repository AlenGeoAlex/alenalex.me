using AlenAlex.Api.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace AlenAlex.Api.Infrastructure.Http;

/// <summary>
/// Unreadable request bodies become 400 (or 415), anything else 500. Details are logged, never sent.
/// </summary>
public static class ApiExceptionHandler
{
    public static ExceptionHandlerOptions Options => new()
    {
        ExceptionHandler = HandleAsync,
        // Malformed client input isn't worth an error-level log entry.
        SuppressDiagnosticsCallback = context => context.Exception is BadHttpRequestException,
    };

    private static async Task HandleAsync(HttpContext context)
    {
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var (status, message) = error is BadHttpRequestException badRequest
            ? (badRequest.StatusCode, "bad request")
            : (StatusCodes.Status500InternalServerError, "internal error");

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(message), AppJson.Context.ErrorResponse);
    }
}
