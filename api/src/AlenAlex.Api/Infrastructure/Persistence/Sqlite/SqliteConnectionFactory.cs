using Microsoft.Data.Sqlite;

namespace AlenAlex.Api.Infrastructure.Persistence.Sqlite;

public sealed class SqliteConnectionFactory
{
    public const string ConnectionStringName = "Guestbook";

    public SqliteConnectionFactory(IConfiguration configuration)
        : this(configuration.GetConnectionString(ConnectionStringName)
               ?? throw new InvalidOperationException($"Missing connection string ConnectionStrings:{ConnectionStringName}"))
    {
    }

    public SqliteConnectionFactory(string connectionString)
    {
        // Off by default in SQLite; likes cascade-delete with their entry.
        ConnectionString = new SqliteConnectionStringBuilder(connectionString) { ForeignKeys = true }.ToString();
    }

    public string ConnectionString { get; }

    public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
