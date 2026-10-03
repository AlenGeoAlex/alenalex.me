using AlenAlex.Generator.Configuration;
using AlenAlex.Generator.Storage;
using Microsoft.Extensions.Configuration;

namespace AlenAlex.Generator.Tests;

public class ConfigurationTests
{
    private static IConfiguration Build(Dictionary<string, string?> appsettings, Dictionary<string, string?> env) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(appsettings)
            .AddInMemoryCollection(env)
            .Build();

    [Fact]
    public void R2_settings_bind_from_appsettings()
    {
        var config = Build(new()
        {
            ["R2:AccountId"] = "acc", ["R2:AccessKey"] = "key", ["R2:SecretKey"] = "secret",
            ["R2:Bucket"] = "bucket", ["R2:PublicUrlBase"] = "https://assets.alenalex.me/",
        }, new());

        var options = R2Options.FromConfiguration(config, out var missing);

        Assert.Empty(missing);
        Assert.NotNull(options);
        Assert.Equal("https://assets.alenalex.me", options.PublicUrlBase);
        Assert.Equal(new Uri("https://acc.r2.cloudflarestorage.com"), options.Endpoint);
    }

    [Fact]
    public void Environment_overrides_appsettings()
    {
        var config = Build(
            new() { ["R2:AccountId"] = "from-file", ["R2:AccessKey"] = "k", ["R2:SecretKey"] = "s", ["R2:Bucket"] = "b" },
            new() { ["R2:AccountId"] = "from-env" });

        Assert.Equal("from-env", R2Options.FromConfiguration(config, out _)!.AccountId);
    }

    [Fact]
    public void Real_environment_variables_use_the_double_underscore_form()
    {
        const string name = "R2__Bucket";
        Environment.SetEnvironmentVariable(name, "env-bucket");
        try
        {
            var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            Assert.Equal("env-bucket", config["R2:Bucket"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void Missing_or_blank_keys_are_reported_by_their_configuration_name()
    {
        var config = Build(new() { ["R2:AccountId"] = "acc", ["R2:SecretKey"] = "  " }, new());

        Assert.Null(R2Options.FromConfiguration(config, out var missing));
        Assert.Equal(["R2:AccessKey", "R2:SecretKey", "R2:Bucket"], missing);
    }

    [Fact]
    public void Posts_section_binds_directory_and_changed()
    {
        var config = Build(new() { ["Posts:Directory"] = "../blogs" }, new() { ["Posts:Changed"] = "test-blog" });
        var posts = config.GetSection(PostsOptions.SectionName).Get<PostsOptions>()!;

        Assert.Equal("../blogs", posts.Directory);
        Assert.Equal("test-blog", posts.Changed);
    }
}
