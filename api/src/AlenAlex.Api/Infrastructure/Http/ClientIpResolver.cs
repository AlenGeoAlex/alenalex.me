using System.Net;
using System.Security.Cryptography;
using System.Text;
using AlenAlex.Api.Options;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Infrastructure.Http;

public interface IClientIpResolver
{
    /// <summary>
    /// The <c>Api:IpHeader</c> header if set, else the TCP peer, else <c>0.0.0.0</c>.
    /// </summary>
    IPAddress GetClientIp(HttpContext context);

    string GetIpHash(HttpContext context);
}

/// <inheritdoc cref="IClientIpResolver"/>
public sealed class ClientIpResolver(IOptions<ApiOptions> options) : IClientIpResolver
{
    public IPAddress GetClientIp(HttpContext context)
    {
        var header = options.Value.IpHeader;
        if (!string.IsNullOrWhiteSpace(header)
            && context.Request.Headers.TryGetValue(header, out var values)
            && values.FirstOrDefault() is { } raw
            // X-Forwarded-For style headers can hold a list; the first is the client.
            && IPAddress.TryParse(raw.Split(',')[0].Trim(), out var fromHeader))
        {
            return fromHeader;
        }

        var remote = context.Connection.RemoteIpAddress;
        if (remote is null)
        {
            return IPAddress.Any;
        }

        // Kestrel listens dual-stack, so IPv4 peers show up as ::ffff:a.b.c.d. Stored hashes
        // are of the plain IPv4 form.
        return remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote;
    }

    public string GetIpHash(HttpContext context) => IpHasher.Hash(options.Value.HashingSalt, GetClientIp(context));
}

public static class IpHasher
{
    /// <summary>
    /// <c>hex(sha256(salt + ip))</c>. Don't change the scheme or the salt: stored likes, rate
    /// limits and pending-entry ownership are keyed by these hashes.
    /// </summary>
    public static string Hash(string salt, IPAddress ip) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(salt + ip.ToString())));
}
