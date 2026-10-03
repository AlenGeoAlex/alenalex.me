using AlenAlex.Api.Infrastructure.Http;

namespace AlenAlex.Api.Features.Guestbook.Shared;

public abstract class GuestbookException(string message) : Exception(message);

/// <summary>Invalid name or message (HTTP 422).</summary>
public sealed class GuestbookValidationException(string message) : GuestbookException(message);

/// <summary>Too many entries from one IP hash in the last hour (HTTP 429).</summary>
public sealed class GuestbookRateLimitedException() : GuestbookException("too many entries, please try again later");

/// <summary>No such entry, or not in the required state (HTTP 404).</summary>
public sealed class GuestbookEntryNotFoundException() : GuestbookException("entry not found");

public static class GuestbookErrorFilter
{
    public static async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (GuestbookException ex)
        {
            var status = ex switch
            {
                GuestbookValidationException => StatusCodes.Status422UnprocessableEntity,
                GuestbookRateLimitedException => StatusCodes.Status429TooManyRequests,
                GuestbookEntryNotFoundException => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status500InternalServerError,
            };
            return ApiErrors.Create(status, ex.Message);
        }
    }
}
