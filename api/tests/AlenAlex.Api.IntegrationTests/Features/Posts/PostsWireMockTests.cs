using System.Net;
using System.Text.Json.Nodes;
using AlenAlex.Api.IntegrationTests.Support;
using static AlenAlex.Api.IntegrationTests.Support.Json;

namespace AlenAlex.Api.IntegrationTests.Features.Posts;

[Collection(WireMockCollection.Name)]
public sealed class PostsWireMockTests(WireMockFixture wireMock) : IAsyncLifetime
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";
    private const string Repo = "/repos/AlenGeoAlex/alenalex.me";
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 255];

    private ApiFactory? _factory;

    private static string RawFile(string path, string gitRef, string body) =>
        $$"""
        { "request": { "method": "GET", "urlPath": "{{Repo}}/contents/blogs/{{path}}",
            "queryParameters": { "ref": { "equalTo": "{{gitRef}}" } },
            "headers": { "Accept": { "contains": "application/vnd.github.raw" }, "Authorization": { "equalTo": "Bearer test-token" } } },
          "response": { "status": 200, "body": {{JsonValue.Create(body).ToJsonString()}} } }
        """;

    private static string Commit(string sha, string message) =>
        $$"""
        { "sha": "{{sha}}", "html_url": "https://github.com/AlenGeoAlex/alenalex.me/commit/{{sha}}",
          "commit": { "message": {{JsonValue.Create(message).ToJsonString()}}, "author": { "date": "2026-10-01T09:30:00Z" }, "committer": null } }
        """;

    private static string CommitsPage(string folder, int page, string[] commits) =>
        $$"""
        { "request": { "method": "GET", "urlPath": "{{Repo}}/commits",
            "queryParameters": { "path": { "equalTo": "blogs/{{folder}}/index.md" }, "page": { "equalTo": "{{page}}" } } },
          "response": { "status": 200, "headers": { "Content-Type": "application/json" }, "jsonBody": [ {{string.Join(",", commits)}} ] } }
        """;

    public async ValueTask InitializeAsync()
    {
        if (wireMock.SkipReason is not null)
        {
            return;
        }
        await wireMock.ResetAsync();
        await wireMock.StubAsync(
            $$"""
            { "request": { "method": "GET", "urlPath": "{{Repo}}/commits",
                "queryParameters": { "path": { "equalTo": "blogs/hello/index.md" }, "per_page": { "equalTo": "100" }, "page": { "equalTo": "1" } } },
              "response": { "status": 200, "headers": { "Content-Type": "application/json" }, "jsonBody": [
                { "sha": "{{Sha}}", "html_url": "https://github.com/AlenGeoAlex/alenalex.me/commit/{{Sha}}",
                  "commit": { "message": "Rewrite intro\n\nDetails", "author": { "date": "2026-10-01T09:30:00Z" }, "committer": { "date": "2026-10-02T09:30:00Z" } } },
                { "sha": "5555555555555555555555555555555555555555", "html_url": "https://github.com/AlenGeoAlex/alenalex.me/commit/5555555",
                  "commit": { "message": "Fix typo\n\n[Skip Rev]", "author": { "date": "2026-09-20T09:30:00Z" }, "committer": null } },
                { "sha": "fedcba9876543210fedcba9876543210fedcba98", "html_url": "https://github.com/AlenGeoAlex/alenalex.me/commit/fedcba9",
                  "commit": { "message": "First draft", "author": { "date": "2026-09-01T09:30:00Z" }, "committer": null } } ] } }
            """);
        await wireMock.StubAsync(
            $$"""
            { "request": { "method": "GET", "urlPath": "{{Repo}}/commits", "queryParameters": { "path": { "equalTo": "blogs/limited/index.md" } } },
              "response": { "status": 403, "headers": { "Content-Type": "application/json", "x-ratelimit-remaining": "0" },
                "jsonBody": { "message": "API rate limit exceeded" } } }
            """);
        await wireMock.StubAsync(RawFile("hello/.meta", "main", "title: Hello"));
        await wireMock.StubAsync(RawFile("hello/index.md", "main", "# Hello\n"));
        await wireMock.StubAsync(RawFile("hello/.meta", Sha, "title: Old"));
        await wireMock.StubAsync(RawFile("hello/index.md", Sha, "# Old\n"));
        await wireMock.StubAsync(
            $$"""
            { "request": { "method": "GET", "urlPath": "{{Repo}}/contents/blogs/hello/assets/logo.png", "queryParameters": { "ref": { "equalTo": "{{Sha}}" } } },
              "response": { "status": 200, "headers": { "Content-Type": "application/octet-stream" }, "base64Body": "{{Convert.ToBase64String(Png)}}" } }
            """);
        await wireMock.StubAsync(
            $$"""
            { "request": { "method": "GET", "urlPath": "{{Repo}}/contents/blogs/hello/assets/huge.png" },
              "response": { "status": 200, "base64Body": "{{Convert.ToBase64String(new byte[10 * 1024 * 1024 + 1])}}" } }
            """);
        // revisions-since: GitHub only returns the commits from that day on
        await wireMock.StubAsync(
            $$"""
            { "priority": 1,
              "request": { "method": "GET", "urlPath": "{{Repo}}/commits",
                "queryParameters": { "path": { "equalTo": "blogs/hello/index.md" }, "since": { "equalTo": "2026-09-15T00:00:00Z" } } },
              "response": { "status": 200, "headers": { "Content-Type": "application/json" }, "jsonBody": [
                { "sha": "{{Sha}}", "html_url": "https://github.com/AlenGeoAlex/alenalex.me/commit/{{Sha}}",
                  "commit": { "message": "Rewrite intro", "author": { "date": "2026-10-01T09:30:00Z" }, "committer": null } } ] } }
            """);
        // A full first page of skipped commits: the list is filled from the next page.
        await wireMock.StubAsync(CommitsPage("busy", 1, Enumerable.Range(0, 100).Select(i => Commit($"{i:x40}", "Tweak [skip rev]")).ToArray()));
        await wireMock.StubAsync(CommitsPage("busy", 2, [Commit(Sha, "Real edit")]));
        // A series: blogs/my-series/series.meta, a cover, and part folders with their own assets.
        await wireMock.StubAsync(
            $$"""
            { "request": { "method": "GET", "urlPath": "{{Repo}}/commits",
                "queryParameters": { "path": { "equalTo": "blogs/my-series/part-1/index.md" }, "per_page": { "equalTo": "100" } } },
              "response": { "status": 200, "headers": { "Content-Type": "application/json" }, "jsonBody": [
                { "sha": "{{Sha}}", "html_url": "https://github.com/AlenGeoAlex/alenalex.me/commit/{{Sha}}",
                  "commit": { "message": "Part one", "author": { "date": "2026-10-03T08:00:00Z" }, "committer": null } } ] } }
            """);
        await wireMock.StubAsync(RawFile("my-series/part-1/.meta", "main", "title: Part 1"));
        await wireMock.StubAsync(RawFile("my-series/part-1/index.md", "main", "# Part 1\n"));
        await wireMock.StubAsync(RawFile("my-series/part-1/.meta", Sha, "title: Part 1 (old)"));
        await wireMock.StubAsync(RawFile("my-series/part-1/index.md", Sha, "# Part 1 old\n"));
        foreach (var asset in new[] { "my-series/assets/cover.png", "my-series/part-1/assets/diagram.png" })
        {
            await wireMock.StubAsync(
                $$"""
                { "request": { "method": "GET", "urlPath": "{{Repo}}/contents/blogs/{{asset}}" },
                  "response": { "status": 200, "base64Body": "{{Convert.ToBase64String(Png)}}" } }
                """);
        }
        // Unmapped paths (e.g. blogs/nope/...) get WireMock's default 404, like GitHub.

        _factory = new ApiFactory(new Dictionary<string, string?>
        {
            ["Github:Token"] = "test-token",
            ["Github:ApiUrl"] = $"{wireMock.BaseUrl}/",
            ["Github:GraphQlUrl"] = $"{wireMock.BaseUrl}/graphql",
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private void SkipWithoutDocker()
    {
        if (wireMock.SkipReason is { } reason) Assert.Skip(reason);
    }

    [Fact]
    public async Task Lists_revisions_newest_first_and_caches_them()
    {
        SkipWithoutDocker();
        var client = _factory!.CreateClient();

        var response = await client.GetAsync("/api/posts/hello/revisions", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=600", response.Headers.CacheControl!.ToString());
        AssertEqual(
            $$"""
            { "revisions": [
              { "sha": "{{Sha}}", "shortSha": "0123456", "date": "2026-10-01", "message": "Rewrite intro",
                "url": "https://github.com/AlenGeoAlex/alenalex.me/commit/{{Sha}}" },
              { "sha": "fedcba9876543210fedcba9876543210fedcba98", "shortSha": "fedcba9", "date": "2026-09-01", "message": "First draft",
                "url": "https://github.com/AlenGeoAlex/alenalex.me/commit/fedcba9" } ] }
            """, JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));

        await client.GetAsync("/api/posts/hello/revisions", TestContext.Current.CancellationToken);
        Assert.Equal(1, await wireMock.CountRequestsAsync($$"""{ "method": "GET", "urlPath": "{{Repo}}/commits" }"""));
    }

    [Fact]
    public async Task Revisions_start_at_since()
    {
        SkipWithoutDocker();
        var (status, body) = await GetAsync(_factory!.CreateClient(), "/api/posts/hello/revisions?since=2026-09-15");
        Assert.Equal(HttpStatusCode.OK, status);
        AssertEqual(
            $$"""
            { "revisions": [ { "sha": "{{Sha}}", "shortSha": "0123456", "date": "2026-10-01", "message": "Rewrite intro",
              "url": "https://github.com/AlenGeoAlex/alenalex.me/commit/{{Sha}}" } ] }
            """, body);
    }

    [Theory]
    [InlineData("yesterday")]
    [InlineData("2026-13-01")]
    [InlineData("2026-10-03T00:00:00Z")]
    public async Task A_bad_since_is_400(string since)
    {
        SkipWithoutDocker();
        var (status, _) = await GetAsync(_factory!.CreateClient(), $"/api/posts/hello/revisions?since={Uri.EscapeDataString(since)}");
        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Skipped_commits_are_filled_from_the_next_page()
    {
        SkipWithoutDocker();
        var (status, body) = await GetAsync(_factory!.CreateClient(), "/api/posts/busy/revisions");
        Assert.Equal(HttpStatusCode.OK, status);
        AssertEqual(
            $$"""
            { "revisions": [ { "sha": "{{Sha}}", "shortSha": "0123456", "date": "2026-10-01", "message": "Real edit",
              "url": "https://github.com/AlenGeoAlex/alenalex.me/commit/{{Sha}}" } ] }
            """, body);
    }

    [Fact]
    public async Task Returns_source_at_main_by_default()
    {
        SkipWithoutDocker();
        var response = await _factory!.CreateClient().GetAsync("/api/posts/hello/source", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertEqual("""{ "ref": "main", "meta": "title: Hello", "markdown": "# Hello\n" }""", JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        Assert.Equal("public, max-age=120", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Source_at_a_sha_is_cached_and_served_with_long_caching()
    {
        SkipWithoutDocker();
        var client = _factory!.CreateClient();

        for (var i = 0; i < 2; i++)
        {
            var response = await client.GetAsync($"/api/posts/hello/source?ref={Sha}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertEqual($$"""{ "ref": "{{Sha}}", "meta": "title: Old", "markdown": "# Old\n" }""", JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
            Assert.Equal("public, max-age=86400", response.Headers.CacheControl!.ToString());
        }

        // The second call never reached GitHub.
        Assert.Equal(1, await wireMock.CountRequestsAsync($$"""{ "method": "GET", "urlPath": "{{Repo}}/contents/blogs/hello/.meta", "queryParameters": { "ref": { "equalTo": "{{Sha}}" } } }"""));
        Assert.Equal(1, await wireMock.CountRequestsAsync($$"""{ "method": "GET", "urlPath": "{{Repo}}/contents/blogs/hello/index.md", "queryParameters": { "ref": { "equalTo": "{{Sha}}" } } }"""));
    }

    [Fact]
    public async Task Missing_post_is_404()
    {
        SkipWithoutDocker();
        var (status, body) = await GetAsync(_factory!.CreateClient(), "/api/posts/nope/source");
        Assert.Equal(HttpStatusCode.NotFound, status);
        AssertEqual("""{"error":"post not found"}""", body);
    }

    [Fact]
    public async Task Rate_limited_github_is_502()
    {
        SkipWithoutDocker();
        var (status, body) = await GetAsync(_factory!.CreateClient(), "/api/posts/limited/revisions");
        Assert.Equal(HttpStatusCode.BadGateway, status);
        AssertEqual("""{"error":"github unavailable"}""", body);
    }

    [Fact]
    public async Task Serves_binary_assets_with_content_type_and_caching()
    {
        SkipWithoutDocker();
        var client = _factory!.CreateClient();

        for (var i = 0; i < 2; i++)
        {
            var response = await client.GetAsync($"/api/posts/hello/assets/logo.png?ref={Sha}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
            Assert.Equal("public, max-age=86400", response.Headers.CacheControl!.ToString());
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        }

        Assert.Equal(1, await wireMock.CountRequestsAsync($$"""{ "method": "GET", "urlPath": "{{Repo}}/contents/blogs/hello/assets/logo.png" }"""));
    }

    [Fact]
    public async Task Rejects_assets_over_10_mb()
    {
        SkipWithoutDocker();
        var (status, body) = await GetAsync(_factory!.CreateClient(), "/api/posts/hello/assets/huge.png");
        Assert.Equal(HttpStatusCode.BadGateway, status);
        AssertEqual("""{"error":"asset too large"}""", body);
    }

    [Fact]
    public async Task Missing_asset_is_404()
    {
        SkipWithoutDocker();
        var (status, body) = await GetAsync(_factory!.CreateClient(), "/api/posts/hello/assets/missing.webp");
        Assert.Equal(HttpStatusCode.NotFound, status);
        AssertEqual("""{"error":"asset not found"}""", body);
    }

    // Series: parts live at blogs/{series}/{part}

    [Fact]
    public async Task Lists_revisions_of_a_series_part_and_caches_them()
    {
        SkipWithoutDocker();
        var client = _factory!.CreateClient();

        for (var i = 0; i < 2; i++)
        {
            var response = await client.GetAsync("/api/posts/my-series/part-1/revisions", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("public, max-age=600", response.Headers.CacheControl!.ToString());
            AssertEqual(
                $$"""
                { "revisions": [ { "sha": "{{Sha}}", "shortSha": "0123456", "date": "2026-10-03", "message": "Part one",
                  "url": "https://github.com/AlenGeoAlex/alenalex.me/commit/{{Sha}}" } ] }
                """, JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        }

        Assert.Equal(1, await wireMock.CountRequestsAsync(
            $$"""{ "method": "GET", "urlPath": "{{Repo}}/commits", "queryParameters": { "path": { "equalTo": "blogs/my-series/part-1/index.md" } } }"""));
    }

    [Fact]
    public async Task Returns_the_source_of_a_series_part()
    {
        SkipWithoutDocker();
        var response = await _factory!.CreateClient().GetAsync("/api/posts/my-series/part-1/source", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=120", response.Headers.CacheControl!.ToString());
        AssertEqual("""{ "ref": "main", "meta": "title: Part 1", "markdown": "# Part 1\n" }""",
            JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Series_part_source_at_a_sha_is_cached()
    {
        SkipWithoutDocker();
        var client = _factory!.CreateClient();

        for (var i = 0; i < 2; i++)
        {
            var response = await client.GetAsync($"/api/posts/my-series/part-1/source?ref={Sha}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("public, max-age=86400", response.Headers.CacheControl!.ToString());
            AssertEqual($$"""{ "ref": "{{Sha}}", "meta": "title: Part 1 (old)", "markdown": "# Part 1 old\n" }""",
                JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        }

        Assert.Equal(1, await wireMock.CountRequestsAsync(
            $$"""{ "method": "GET", "urlPath": "{{Repo}}/contents/blogs/my-series/part-1/index.md", "queryParameters": { "ref": { "equalTo": "{{Sha}}" } } }"""));
    }

    [Fact]
    public async Task Serves_a_series_part_asset_and_caches_it_for_sha_refs()
    {
        SkipWithoutDocker();
        var client = _factory!.CreateClient();

        for (var i = 0; i < 2; i++)
        {
            var response = await client.GetAsync($"/api/posts/my-series/part-1/assets/diagram.png?ref={Sha}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
            Assert.Equal("public, max-age=86400", response.Headers.CacheControl!.ToString());
            Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        }

        Assert.Equal(1, await wireMock.CountRequestsAsync(
            $$"""{ "method": "GET", "urlPath": "{{Repo}}/contents/blogs/my-series/part-1/assets/diagram.png" }"""));
    }

    // Series-level assets (the cover) use the single-folder asset route.
    [Fact]
    public async Task Serves_a_series_level_asset_through_the_folder_asset_route()
    {
        SkipWithoutDocker();
        var response = await _factory!.CreateClient().GetAsync("/api/posts/my-series/assets/cover.png", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await wireMock.CountRequestsAsync(
            $$"""{ "method": "GET", "urlPath": "{{Repo}}/contents/blogs/my-series/assets/cover.png" }"""));
    }

    [Fact]
    public async Task Missing_series_part_is_404()
    {
        SkipWithoutDocker();
        var (status, body) = await GetAsync(_factory!.CreateClient(), "/api/posts/my-series/part-9/source");
        Assert.Equal(HttpStatusCode.NotFound, status);
        AssertEqual("""{"error":"post not found"}""", body);
    }
}
