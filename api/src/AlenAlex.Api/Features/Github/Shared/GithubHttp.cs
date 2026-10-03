using System.Net.Http.Headers;
using AlenAlex.Api.Options;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Features.Github.Shared;

public static class GithubHttp
{
    public static void Configure(IServiceProvider services, HttpClient client)
    {
        var options = services.GetRequiredService<IOptions<GithubOptions>>().Value;
        client.BaseAddress = new Uri(options.ApiUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(20);
        // GitHub rejects requests without a User-Agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("alenalex.me-api");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        if (options.IsConfigured)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        }
    }
}
