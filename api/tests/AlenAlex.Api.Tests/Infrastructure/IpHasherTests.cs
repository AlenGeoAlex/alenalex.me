using System.Net;
using AlenAlex.Api.Infrastructure.Http;
using AlenAlex.Api.Options;
using Microsoft.AspNetCore.Http;

namespace AlenAlex.Api.Tests.Infrastructure;

public sealed class IpHasherTests
{
    // Stored likes and pending-entry ownership are keyed by hex(sha256(salt + ip)).
    // If this breaks, existing visitors lose their likes and pending entries.
    [Theory]
    [InlineData("a", "203.0.113.7", "585eb0382b4d8ae8297465a89b34cc7fd892d4934601eaeba23b02111ce11a6e")]
    [InlineData("test", "2001:db8::1", "203a2afa494c8d9dd16ab2048f0ebbde1d5fcb8d73dfac50a8729e2291066867")]
    [InlineData("test", "1.1.1.1", "d0d2ef2628564a50159cff95d52c25eaf85d90427acc561fc058f1b80298f330")]
    public void Hash_is_stable_for_existing_data(string salt, string ip, string expected) =>
        Assert.Equal(expected, IpHasher.Hash(salt, IPAddress.Parse(ip)));

    [Fact]
    public void Is_salted()
    {
        var ip = IPAddress.Parse("203.0.113.7");
        Assert.NotEqual(IpHasher.Hash("a", ip), IpHasher.Hash("b", ip));
        Assert.Equal(64, IpHasher.Hash("a", ip).Length);
    }
}

public sealed class ClientIpResolverTests
{
    private static ClientIpResolver Resolver(string? ipHeader) =>
        new(Microsoft.Extensions.Options.Options.Create(new ApiOptions { HashingSalt = "s", IpHeader = ipHeader }));

    private static DefaultHttpContext Request(string remote, string? header = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        if (header is not null)
        {
            context.Request.Headers["CF-Connecting-IP"] = header;
        }
        return context;
    }

    [Fact]
    public void Uses_the_configured_header_first() =>
        Assert.Equal(IPAddress.Parse("203.0.113.7"), Resolver("CF-Connecting-IP").GetClientIp(Request("10.0.0.1", "203.0.113.7")));

    [Fact]
    public void Takes_the_first_address_of_a_list() =>
        Assert.Equal(IPAddress.Parse("203.0.113.7"), Resolver("CF-Connecting-IP").GetClientIp(Request("10.0.0.1", "203.0.113.7, 10.0.0.2")));

    [Theory]
    [InlineData(null)]
    [InlineData("not-an-ip")]
    public void Falls_back_to_the_peer_address(string? header) =>
        Assert.Equal(IPAddress.Parse("10.0.0.1"), Resolver("CF-Connecting-IP").GetClientIp(Request("10.0.0.1", header)));

    [Fact]
    public void Ignores_the_header_unless_configured() =>
        Assert.Equal(IPAddress.Parse("10.0.0.1"), Resolver(null).GetClientIp(Request("10.0.0.1", "203.0.113.7")));

    [Fact]
    public void Maps_dual_stack_ipv4_peers_to_plain_ipv4() =>
        Assert.Equal("10.0.0.1", Resolver(null).GetClientIp(Request("::ffff:10.0.0.1")).ToString());
}
