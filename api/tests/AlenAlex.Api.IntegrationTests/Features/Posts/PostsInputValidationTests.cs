using System.Net;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Nodes;
using AlenAlex.Api.IntegrationTests.Support;
using static AlenAlex.Api.IntegrationTests.Support.Json;

namespace AlenAlex.Api.IntegrationTests.Features.Posts;

public sealed class PostsInputValidationTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Theory]
    [InlineData("/api/posts/a%20b/revisions")]
    [InlineData("/api/posts/hello/source?ref=a..b")]
    [InlineData("/api/posts/hello/source?ref=..%2Fmain")]
    [InlineData("/api/posts/hello/source?ref=main%3Bx")]
    [InlineData("/api/posts/hello/assets/page.html")]
    [InlineData("/api/posts/hello/assets/logo.png?ref=a..b")]
    [InlineData("/api/posts/my-series/a%20b/revisions")]
    [InlineData("/api/posts/a%20b/part-1/source")]
    [InlineData("/api/posts/my-series/part-1/source?ref=a..b")]
    [InlineData("/api/posts/my-series/part-1/assets/page.html")]
    [InlineData("/api/posts/my-series/part-1/assets/a%20b.png")]
    [InlineData("/api/posts/my-series/part-1/assets/logo.png?ref=..%2Fmain")]
    public async Task Rejects_bad_input(string uri)
    {
        var (status, body) = await GetAsync(_factory.CreateClient(), uri);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        AssertEqual("""{"error":"bad request"}""", body);
    }

    // HttpClient and Kestrel normalise dot segments away, so send them straight into the pipeline.
    [Theory]
    [InlineData("/api/posts/../revisions", "")]
    [InlineData("/api/posts/./source", "")]
    [InlineData("/api/posts/hello/assets/..", "?ref=main")]
    [InlineData("/api/posts/../part-1/revisions", "")]
    [InlineData("/api/posts/my-series/../source", "")]
    [InlineData("/api/posts/my-series/./revisions", "")]
    [InlineData("/api/posts/my-series/part-1/assets/..", "?ref=main")]
    [InlineData("/api/posts/../../assets/x.png", "")]
    public async Task Rejects_dot_segments(string path, string query)
    {
        var context = await _factory.Server.SendAsync(c =>
        {
            c.Request.Method = "GET";
            c.Request.Path = path;
            c.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString(query.Length == 0 ? null : query);
        }, TestContext.Current.CancellationToken);

        Assert.Equal(400, context.Response.StatusCode);
        AssertEqual("""{"error":"bad request"}""", await JsonNode.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/api/posts/hello/revisions")]
    [InlineData("/api/posts/hello/source")]
    [InlineData("/api/posts/hello/source?ref=feature/x")]
    [InlineData("/api/posts/hello/assets/logo.png")]
    [InlineData("/api/posts/my-series/part-1/revisions")]
    [InlineData("/api/posts/my-series/part-1/source")]
    [InlineData("/api/posts/my-series/part-1/assets/logo.png")]
    [InlineData("/api/posts/my-series/assets/cover.png")]
    public async Task Needs_a_github_token(string uri)
    {
        var (status, body) = await GetAsync(_factory.CreateClient(), uri);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        AssertEqual("""{"error":"github not configured"}""", body);
    }

    // The part routes must not swallow the single-folder asset route.
    [Theory]
    [InlineData("/api/posts/a/assets/x.png", "HTTP: GET /api/posts/{folder}/assets/{file}")]
    [InlineData("/api/posts/a/assets/revisions", "HTTP: GET /api/posts/{folder}/assets/{file}")]
    [InlineData("/api/posts/a/assets/source", "HTTP: GET /api/posts/{folder}/assets/{file}")]
    [InlineData("/api/posts/a/b/revisions", "HTTP: GET /api/posts/{series}/{part}/revisions")]
    [InlineData("/api/posts/a/b/source", "HTTP: GET /api/posts/{series}/{part}/source")]
    [InlineData("/api/posts/a/b/assets/x.png", "HTTP: GET /api/posts/{series}/{part}/assets/{file}")]
    [InlineData("/api/posts/a/revisions", "HTTP: GET /api/posts/{folder}/revisions")]
    [InlineData("/api/posts/a/source", "HTTP: GET /api/posts/{folder}/source")]
    public async Task Routes_to_the_intended_endpoint(string path, string endpoint)
    {
        var context = await _factory.Server.SendAsync(c =>
        {
            c.Request.Method = "GET";
            c.Request.Path = path;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(endpoint, context.GetEndpoint()?.DisplayName);
    }

    [Fact]
    public async Task Asset_route_rejects_non_image_names_that_look_like_part_actions()
    {
        // /a/assets/revisions is the asset route with file "revisions", not a part called "assets".
        var (status, body) = await GetAsync(_factory.CreateClient(), "/api/posts/a/assets/revisions");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        AssertEqual("""{"error":"bad request"}""", body);
    }
}

