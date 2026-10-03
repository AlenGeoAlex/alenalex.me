using Microsoft.Extensions.Configuration;

namespace AlenAlex.Generator.Configuration;

/// <summary>
/// Later sources win: <c>appsettings.json</c> (committed, no secrets), <c>appsettings.Development.json</c>
/// (gitignored, local R2 keys), then environment variables such as <c>R2__AccountId</c>.
/// </summary>
internal static class GeneratorConfiguration
{
    public static IConfigurationRoot Build() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
}
