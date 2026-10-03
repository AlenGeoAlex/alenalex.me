using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>How often to run <c>SELECT 1</c> so a managed database doesn't power off. Zero disables it.</summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromHours(1);
}

public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options) =>
        options.KeepAliveInterval < TimeSpan.Zero
            ? ValidateOptionsResult.Fail("Database:KeepAliveInterval must be zero (off) or positive")
            : options.KeepAliveInterval > TimeSpan.Zero && options.KeepAliveInterval < TimeSpan.FromSeconds(10)
                ? ValidateOptionsResult.Fail("Database:KeepAliveInterval must be at least 00:00:10")
                : ValidateOptionsResult.Success;
}
