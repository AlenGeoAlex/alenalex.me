using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using AlenAlex.Api.Features.Github.Shared;
using AlenAlex.Api.Features.Guestbook.CreateEntry;
using AlenAlex.Api.Features.Guestbook.ListEntries;
using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Features.Posts.GetRevisions;
using AlenAlex.Api.Features.Posts.GetSource;
using AlenAlex.Api.Features.Status.GetStatus;
using AlenAlex.Api.Infrastructure.Http;

namespace AlenAlex.Api.Json;

/// <summary>
/// Every request/response type the API exposes. GitHub payloads use their own contexts
/// (Features/Github, Features/Posts) because GitHub's REST API is snake_case.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ListEntriesResponse))]
[JsonSerializable(typeof(CreateEntryRequest))]
[JsonSerializable(typeof(CreateEntryResponse))]
[JsonSerializable(typeof(LikeResponse))]
[JsonSerializable(typeof(GithubSummary))]
[JsonSerializable(typeof(GetStatusResponse))]
[JsonSerializable(typeof(GetRevisionsResponse))]
[JsonSerializable(typeof(GetSourceResponse))]
public sealed partial class AppJsonContext : JsonSerializerContext;

public static class AppJson
{
    /// <summary>Leaves non-ASCII text unescaped; HTML-sensitive characters are still escaped.</summary>
    public static AppJsonContext Context { get; } = new(new JsonSerializerOptions(AppJsonContext.Default.Options)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    });
}
