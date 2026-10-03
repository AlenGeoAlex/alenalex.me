using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>How often to run <c>SELECT 1</c> so a managed database doesn't power off. Zero disables it.</summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Schema for the guestbook tables and FluentMigrator's VersionInfo. Use one the app's role owns
    /// when it can't create tables in <c>public</c> (the default on PostgreSQL 15+ for non-owners).
    /// </summary>
    public string Schema { get; set; } = DefaultSchema;

    public const string DefaultSchema = "public";
}

public sealed partial class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    // a plain, unquoted PostgreSQL identifier
    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")]
    private static partial Regex SchemaName();

    public ValidateOptionsResult Validate(string? name, DatabaseOptions options) =>
        !SchemaName().IsMatch(options.Schema ?? "")
            ? ValidateOptionsResult.Fail("Database:Schema must be a lowercase identifier (letters, digits, underscores)")
        : options.KeepAliveInterval < TimeSpan.Zero
            ? ValidateOptionsResult.Fail("Database:KeepAliveInterval must be zero (off) or positive")
            : options.KeepAliveInterval > TimeSpan.Zero && options.KeepAliveInterval < TimeSpan.FromSeconds(10)
                ? ValidateOptionsResult.Fail("Database:KeepAliveInterval must be at least 00:00:10")
                : ValidateOptionsResult.Success;
}
