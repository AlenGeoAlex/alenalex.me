using AlenAlex.Api.Options;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Extensions;

public static class BootstrapExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder SetupConfigurationOptions()
        {
            builder.Services
                .AddOptions<ApiOptions>()
                .Bind(builder.Configuration.GetSection(ApiOptions.SectionName))
                .ValidateOnStart();
            
            builder.Services
                .AddOptions<GithubOptions>()
                .Bind(builder.Configuration.GetSection(GithubOptions.SectionName))
                .ValidateOnStart();
            
            builder.Services
                .AddOptions<HomelabOptions>()
                .Bind(builder.Configuration.GetSection(HomelabOptions.SectionName))
                .ValidateOnStart();
            
            builder.Services
                .AddOptions<DiscordOptions>()
                .Bind(builder.Configuration.GetSection(DiscordOptions.SectionName));
            
            builder.Services.AddSingleton<IValidateOptions<ApiOptions>, ApiOptionsValidator>();
            builder.Services.AddSingleton<IValidateOptions<GithubOptions>, GithubOptionsValidator>();
            builder.Services.AddSingleton<IValidateOptions<HomelabOptions>, HomelabOptionsValidator>();
            
            return builder;
        }

        public WebApplicationBuilder SetupHostingConfiguration()
        {
            builder.Services
                .Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(10))
                .Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

            return builder;
        }

        public WebApplicationBuilder SetupCors()
        {
            builder.Services.AddCors();
            builder.Services.AddOptions<CorsOptions>().Configure<IOptions<ApiOptions>>((cors, api) =>
                cors.AddDefaultPolicy(policy => policy
                    .WithOrigins(api.Value.AllowedOrigins)
                    .WithMethods("GET", "POST", "DELETE", "OPTIONS")
                    .WithHeaders("Content-Type")));
            return builder;
        }
    }
}