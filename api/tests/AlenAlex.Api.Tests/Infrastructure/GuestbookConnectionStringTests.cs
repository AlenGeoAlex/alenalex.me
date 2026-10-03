using AlenAlex.Api.Infrastructure.Persistence.Postgres;
using Npgsql;

namespace AlenAlex.Api.Tests.Infrastructure;

public sealed class GuestbookConnectionStringTests
{
    [Fact]
    public void Converts_an_aiven_style_uri()
    {
        var builder = new NpgsqlConnectionStringBuilder(GuestbookConnectionString.Normalize(
            "postgres://avnadmin:s3cr%40t%3Ax@pg-abc.aivencloud.com:12345/defaultdb?sslmode=require"));

        Assert.Equal("pg-abc.aivencloud.com", builder.Host);
        Assert.Equal(12345, builder.Port);
        Assert.Equal("defaultdb", builder.Database);
        Assert.Equal("avnadmin", builder.Username);
        Assert.Equal("s3cr@t:x", builder.Password);
        Assert.Equal(SslMode.Require, builder.SslMode);
    }

    [Fact]
    public void Accepts_postgresql_scheme_defaults_and_root_certificate()
    {
        var builder = new NpgsqlConnectionStringBuilder(GuestbookConnectionString.Normalize(
            "postgresql://app@db.local?sslmode=verify-full&sslrootcert=/etc/ssl/aiven-ca.pem"));

        Assert.Equal("db.local", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("postgres", builder.Database);
        Assert.Equal("app", builder.Username);
        Assert.Null(builder.Password);
        Assert.Equal(SslMode.VerifyFull, builder.SslMode);
        Assert.Equal("/etc/ssl/aiven-ca.pem", builder.RootCertificate);
    }

    [Fact]
    public void Leaves_npgsql_connection_strings_unchanged()
    {
        const string value = "Host=localhost;Port=5432;Database=guestbook;Username=postgres;Password=dev";
        Assert.Equal(value, GuestbookConnectionString.Normalize(value));
    }

    [Fact]
    public void Rejects_an_unknown_sslmode() =>
        Assert.Throws<FormatException>(() => GuestbookConnectionString.Normalize("postgres://u:p@h/db?sslmode=sometimes"));
}
