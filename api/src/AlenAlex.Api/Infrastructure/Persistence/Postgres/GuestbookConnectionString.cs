using Npgsql;

namespace AlenAlex.Api.Infrastructure.Persistence.Postgres;

/// <summary>
/// Reads <c>ConnectionStrings:Guestbook</c>. Accepts an Npgsql connection string
/// (<c>Host=…;Username=…</c>) or the URI form managed providers hand out
/// (<c>postgres://user:pass@host:port/db?sslmode=require</c>).
/// </summary>
public static class GuestbookConnectionString
{
    public const string Name = "Guestbook";

    public static string FromConfiguration(IConfiguration configuration)
    {
        var builder = new NpgsqlConnectionStringBuilder(Normalize(configuration.GetConnectionString(Name)
            ?? throw new InvalidOperationException($"Missing connection string ConnectionStrings:{Name}")))
        {
            // unqualified table names in queries resolve to the configured schema
            SearchPath = configuration[$"{AlenAlex.Api.Options.DatabaseOptions.SectionName}:Schema"] is { Length: > 0 } schema
                ? schema
                : AlenAlex.Api.Options.DatabaseOptions.DefaultSchema,
            // Kerberos isn't used; the default "Prefer" probes libgssapi, which the image doesn't ship
            GssEncryptionMode = GssEncryptionMode.Disable,
        };
        return builder.ConnectionString;
    }

    /// <summary>Converts a <c>postgres://</c> / <c>postgresql://</c> URI to Npgsql key-values; anything else is returned unchanged.</summary>
    public static string Normalize(string value)
    {
        value = value.Trim();
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')) is { Length: > 0 } database ? database : "postgres",
        };

        if (uri.UserInfo.Length > 0)
        {
            var separator = uri.UserInfo.IndexOf(':');
            builder.Username = Uri.UnescapeDataString(separator < 0 ? uri.UserInfo : uri.UserInfo[..separator]);
            if (separator >= 0)
            {
                builder.Password = Uri.UnescapeDataString(uri.UserInfo[(separator + 1)..]);
            }
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]);
            var parameter = separator < 0 ? "" : Uri.UnescapeDataString(pair[(separator + 1)..]);
            switch (key.ToLowerInvariant())
            {
                case "sslmode":
                    builder.SslMode = parameter.ToLowerInvariant() switch
                    {
                        "disable" => SslMode.Disable,
                        "allow" => SslMode.Allow,
                        "prefer" => SslMode.Prefer,
                        "require" => SslMode.Require,
                        "verify-ca" => SslMode.VerifyCA,
                        "verify-full" => SslMode.VerifyFull,
                        _ => throw new FormatException($"Unsupported sslmode '{parameter}' in ConnectionStrings:{Name}"),
                    };
                    break;
                case "sslrootcert":
                    builder.RootCertificate = parameter;
                    break;
                case "application_name":
                    builder.ApplicationName = parameter;
                    break;
                // Other libpq parameters have no Npgsql equivalent we need; ignore them.
            }
        }

        return builder.ConnectionString;
    }
}
