using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AlenAlex.Api.Features.Guestbook.ModerateEntry;
using AlenAlex.Api.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using static AlenAlex.Api.IntegrationTests.Support.Json;

namespace AlenAlex.Api.IntegrationTests.Features.Guestbook;

public sealed class GuestbookHttpTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Guestbook_contract()
    {
        var ada = _factory.CreateClientFor("1.1.1.1");
        var bob = _factory.CreateClientFor("2.2.2.2");

        // Validation → 422 with an error message.
        var (status, body) = await SendAsync(ada, HttpMethod.Post, "/api/guestbook", new { name = "Ada", message = "hi" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        AssertEqual("""{"error":"message must be 3–200 characters"}""", body);
        (status, body) = await SendAsync(ada, HttpMethod.Post, "/api/guestbook", new { name = "", message = "hello" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        AssertEqual("""{"error":"name must be 1–40 characters"}""", body);

        // Create → 201 { entry }.
        (status, body) = await SendAsync(ada, HttpMethod.Post, "/api/guestbook", new { name = "Ada", message = "hello world" });
        Assert.Equal(HttpStatusCode.Created, status);
        var entry = body!["entry"]!.AsObject();
        Assert.Equal(["id", "seq", "name", "message", "status", "createdAt", "likeCount", "liked"], entry.Select(p => p.Key));
        Assert.Equal("pending", (string?)entry["status"]);
        Assert.Equal(1, (long)entry["seq"]!);
        Assert.Equal("Ada", (string?)entry["name"]);
        Assert.Equal(0, (long)entry["likeCount"]!);
        Assert.False((bool)entry["liked"]!);
        var createdAt = (string)entry["createdAt"]!;
        // ISO 8601 in UTC (System.Text.Json DateTimeOffset format).
        Assert.Equal(TimeSpan.Zero, DateTimeOffset.Parse(createdAt).Offset);
        var id = (string)entry["id"]!;

        // Queued for Discord moderation.
        var queued = Assert.Single(_factory.Moderation.Queued);
        Assert.Equal(id, queued.Entry.Id);
        Assert.Equal(64, queued.IpHash.Length);

        // Author sees it, others do not.
        (_, body) = await SendAsync(ada, HttpMethod.Get, "/api/guestbook");
        Assert.Single(body!["entries"]!.AsArray());
        (_, body) = await SendAsync(bob, HttpMethod.Get, "/api/guestbook");
        AssertEqual("""{"entries":[]}""", body);

        // Liking a pending entry → 404.
        var likes = $"/api/guestbook/{id}/likes";
        (status, body) = await SendAsync(bob, HttpMethod.Post, likes);
        Assert.Equal(HttpStatusCode.NotFound, status);
        AssertEqual("""{"error":"entry not found"}""", body);

        await _factory.Services.GetRequiredService<ModerateEntryHandler>().ApproveAsync(id, TestContext.Current.CancellationToken);

        (status, body) = await SendAsync(bob, HttpMethod.Post, likes);
        Assert.Equal(HttpStatusCode.OK, status);
        AssertEqual("""{"likeCount":1,"liked":true}""", body);
        (status, body) = await SendAsync(bob, HttpMethod.Post, likes);
        Assert.Equal(HttpStatusCode.OK, status);
        AssertEqual("""{"likeCount":1,"liked":true}""", body);

        (_, body) = await SendAsync(bob, HttpMethod.Get, "/api/guestbook");
        Assert.Equal("accepted", (string?)body!["entries"]![0]!["status"]);
        Assert.True((bool)body["entries"]![0]!["liked"]!);
        Assert.Equal(1, (long)body["entries"]![0]!["likeCount"]!);

        (status, body) = await SendAsync(bob, HttpMethod.Delete, likes);
        Assert.Equal(HttpStatusCode.OK, status);
        AssertEqual("""{"likeCount":0,"liked":false}""", body);
        (_, body) = await SendAsync(bob, HttpMethod.Delete, likes);
        AssertEqual("""{"likeCount":0,"liked":false}""", body);

        (status, body) = await SendAsync(bob, HttpMethod.Delete, "/api/guestbook/nope/likes");
        Assert.Equal(HttpStatusCode.NotFound, status);
        AssertEqual("""{"error":"entry not found"}""", body);

        // Rate limit: 4 more from 1.1.1.1 are fine (5 total), the 6th is 429.
        for (var i = 0; i < 4; i++)
        {
            (status, _) = await SendAsync(ada, HttpMethod.Post, "/api/guestbook", new { name = "Ada", message = $"msg {i}" });
            Assert.Equal(HttpStatusCode.Created, status);
        }
        (status, body) = await SendAsync(ada, HttpMethod.Post, "/api/guestbook", new { name = "Ada", message = "too many" });
        Assert.Equal(HttpStatusCode.TooManyRequests, status);
        AssertEqual("""{"error":"too many entries, please try again later"}""", body);
    }

    [Fact]
    public async Task Rejected_entries_never_reach_clients()
    {
        var ada = _factory.CreateClientFor("1.1.1.1");
        var (_, body) = await SendAsync(ada, HttpMethod.Post, "/api/guestbook", new { name = "Mallory", message = "spam spam" });
        await _factory.Services.GetRequiredService<ModerateEntryHandler>().RejectAsync((string)body!["entry"]!["id"]!, "spam", TestContext.Current.CancellationToken);

        (_, body) = await SendAsync(ada, HttpMethod.Get, "/api/guestbook");
        AssertEqual("""{"entries":[]}""", body);
    }

    [Fact]
    public async Task Malformed_json_is_a_bad_request()
    {
        using var content = new StringContent("{bad", Encoding.UTF8, "application/json");
        var response = await _factory.CreateClientFor("1.1.1.1").PostAsync("/api/guestbook", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertEqual("""{"error":"bad request"}""", JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }


    [Theory]
    [InlineData("'); DROP TABLE guestbook_entries;--")]
    [InlineData("\" OR 1=1 --")]
    [InlineData("$viewer' OR '1'='1")]
    public async Task Injection_payloads_round_trip_verbatim(string payload)
    {
        var ada = _factory.CreateClientFor("1.1.1.1");

        var (status, body) = await SendAsync(ada, HttpMethod.Post, "/api/guestbook", new { name = "Bobby Tables", message = payload });
        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal(payload, (string?)body!["entry"]!["message"]);

        // The table is intact and the entry reads back unchanged.
        (status, body) = await SendAsync(ada, HttpMethod.Get, "/api/guestbook");
        Assert.Equal(HttpStatusCode.OK, status);
        var entry = Assert.Single(body!["entries"]!.AsArray());
        Assert.Equal(payload, (string?)entry!["message"]);

        // A payload as the entry id is just an unknown id.
        (status, body) = await SendAsync(ada, HttpMethod.Post, $"/api/guestbook/{Uri.EscapeDataString(payload)}/likes");
        Assert.Equal(HttpStatusCode.NotFound, status);
        AssertEqual("""{"error":"entry not found"}""", body);
    }
}

