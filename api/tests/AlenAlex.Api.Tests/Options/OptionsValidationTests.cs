using AlenAlex.Api.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Tests.Options;

public sealed class OptionsValidationTests
{
    private static T Bind<T>(string section, IValidateOptions<T> validator, Dictionary<string, string?> values) where T : class
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddOptions<T>().Bind(configuration.GetSection(section));
        services.AddSingleton(validator);
        return services.BuildServiceProvider().GetRequiredService<IOptions<T>>().Value;
    }

    [Fact]
    public void Binds_api_options_with_an_origin_array()
    {
        var options = Bind("Api", new ApiOptionsValidator(), new()
        {
            ["Api:HashingSalt"] = "s",
            ["Api:AllowedOrigins:0"] = "https://alenalex.me",
            ["Api:AllowedOrigins:1"] = "http://localhost:4200",
        });
        Assert.Equal(["https://alenalex.me", "http://localhost:4200"], options.AllowedOrigins);
    }

    [Fact]
    public void Hashing_salt_is_required()
    {
        var ex = Assert.Throws<OptionsValidationException>(() => Bind("Api", new ApiOptionsValidator(), new()));
        Assert.Contains("HashingSalt", ex.Message);
    }

    [Fact]
    public void Binds_homelab_services_as_objects()
    {
        var options = Bind("Homelab", new HomelabOptionsValidator(), new()
        {
            ["Homelab:Services:0:Name"] = "jellyfin",
            ["Homelab:Services:0:Url"] = "http://jellyfin:8096",
            ["Homelab:Services:1:Name"] = "nas",
            ["Homelab:Services:1:Url"] = "https://nas.local",
        });
        Assert.Equal([("jellyfin", "http://jellyfin:8096"), ("nas", "https://nas.local")], options.Services.Select(s => (s.Name, s.Url)));
    }

    [Fact]
    public void Homelab_services_need_a_name_and_an_absolute_url()
    {
        Assert.Throws<OptionsValidationException>(() => Bind("Homelab", new HomelabOptionsValidator(), new()
        {
            ["Homelab:Services:0:Url"] = "http://jellyfin:8096",
        }));
        Assert.Throws<OptionsValidationException>(() => Bind("Homelab", new HomelabOptionsValidator(), new()
        {
            ["Homelab:Services:0:Name"] = "nas",
            ["Homelab:Services:0:Url"] = "not a url",
        }));
    }

    [Fact]
    public void Github_defaults_are_valid_and_unconfigured_without_a_token()
    {
        var options = Bind("Github", new GithubOptionsValidator(), new());
        Assert.False(options.IsConfigured);
        Assert.Equal("AlenGeoAlex/alenalex.me", options.Repo);
    }
}
