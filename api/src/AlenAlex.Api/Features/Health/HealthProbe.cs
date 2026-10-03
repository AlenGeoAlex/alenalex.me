namespace AlenAlex.Api.Features.Health;

/// <summary>
/// <c>alenalex-api --healthcheck</c> calls the running instance's <c>/_health</c> and returns the exit code.
/// The chiseled image has no shell or curl, so the binary checks itself.
/// </summary>
public static class HealthProbe
{
    public const string Argument = "--healthcheck";

    public static async Task<int> RunAsync()
    {
        // May list several ports ("8080;8081"); any of them will do.
        var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split([';', ','])[0].Trim();
        if (string.IsNullOrEmpty(port)) port = "8080";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var response = await http.GetAsync($"http://127.0.0.1:{port}/_health");
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
