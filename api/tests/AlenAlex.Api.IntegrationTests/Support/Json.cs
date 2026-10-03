using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace AlenAlex.Api.IntegrationTests.Support;

public static class Json
{
    public static void AssertEqual(string expected, JsonNode? actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), actual), $"expected {expected} but got {actual?.ToJsonString()}");

    public static async Task<(HttpStatusCode Status, JsonNode? Body)> SendAsync(HttpClient client, HttpMethod method, string uri, object? body = null)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.Length == 0 ? null : JsonNode.Parse(text));
    }

    public static async Task<(HttpStatusCode Status, JsonNode? Body)> GetAsync(HttpClient client, string uri) =>
        await SendAsync(client, HttpMethod.Get, uri);
}
