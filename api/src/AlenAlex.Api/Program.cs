using AlenAlex.Api.Extensions;
using AlenAlex.Api.Features.Github;
using AlenAlex.Api.Features.Guestbook;
using AlenAlex.Api.Features.Health;
using AlenAlex.Api.Features.Posts;
using AlenAlex.Api.Features.Status;
using AlenAlex.Api.Infrastructure.Http;
using AlenAlex.Api.Infrastructure.Persistence;
using AlenAlex.Api.Json;
using AlenAlex.Api.Options;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

// Used by the Docker HEALTHCHECK.
if (args is [HealthProbe.Argument, ..])
{
    Environment.Exit(await HealthProbe.RunAsync());
}

var builder = WebApplication.CreateSlimBuilder(args)
    .SetupConfigurationOptions()
    .SetupHostingConfiguration()
    .SetupCors();

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJson.Context);
    o.SerializerOptions.PropertyNamingPolicy = AppJson.Context.Options.PropertyNamingPolicy;
    o.SerializerOptions.Encoder = AppJson.Context.Options.Encoder;
});

builder.Services.AddOpenApi();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IClientIpResolver, ClientIpResolver>();
builder.Services.AddPersistence();

builder.Services.AddGuestbook();
builder.Services.AddGithub();
builder.Services.AddPosts();
builder.Services.AddStatus();

var app = builder.Build();

var apiOptions = app.Services.GetRequiredService<IOptions<ApiOptions>>().Value;
app.Services.GetRequiredService<DatabaseMigrator>().MigrateUp();
app.Logger.LogInformation("Database ready");
if (apiOptions.AllowedOrigins.Length == 0)
{
    app.Logger.LogWarning("Api:AllowedOrigins is empty: browsers on other origins will be blocked");
}

app.UseExceptionHandler(ApiExceptionHandler.Options);
app.UseCors();

app.MapEndpoint<HealthEndpoint>();
app.MapGuestbook();
app.MapGithub();
app.MapStatus();
app.MapPosts();

if (apiOptions.EnableScalar)
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

await app.RunAsync();

/// <summary>Entry point; public so integration tests can use <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
