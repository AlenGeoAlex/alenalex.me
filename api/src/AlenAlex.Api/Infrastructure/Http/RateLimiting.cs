using System.Globalization;
using System.Threading.RateLimiting;
using AlenAlex.Api.Json;
using Microsoft.AspNetCore.RateLimiting;

namespace AlenAlex.Api.Infrastructure.Http;

/// <summary>
/// Request-rate limit for endpoints that write, per visitor (keyed on the hashed client IP, so it
/// follows <c>Api:IpHeader</c> behind Cloudflare). The guestbook's 5-entries-per-hour rule is separate.
/// </summary>
public static class RateLimiting
{
    public const string WritePolicy = "writes";
    public const int WritesPerMinute = 20;

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse("too many requests, slow down"), AppJson.Context.ErrorResponse, cancellationToken: ct);
            };

            options.AddPolicy(WritePolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.RequestServices.GetRequiredService<IClientIpResolver>().GetIpHash(http),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = WritesPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
}
