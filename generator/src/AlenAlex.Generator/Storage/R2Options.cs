using Microsoft.Extensions.Configuration;

namespace AlenAlex.Generator.Storage;

internal sealed class R2Options
{
    public const string SectionName = "R2";

    public string AccountId { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string? PublicUrlBase { get; set; }

    /// <summary>R2 expects SigV4 requests signed for region <c>auto</c>.</summary>
    public const string Region = "auto";

    public Uri Endpoint => new($"https://{AccountId}.r2.cloudflarestorage.com");

    // Key names for error messages.
    public const string AccountIdKey = "R2:AccountId";
    public const string AccessKeyKey = "R2:AccessKey";
    public const string SecretKeyKey = "R2:SecretKey";
    public const string BucketKey = "R2:Bucket";

    /// <summary>Binds the section; <paramref name="missing"/> lists required keys that are unset or empty.</summary>
    public static R2Options? FromConfiguration(IConfiguration configuration, out IReadOnlyList<string> missing)
    {
        var options = configuration.GetSection(SectionName).Get<R2Options>() ?? new R2Options();
        options.AccountId = options.AccountId.Trim();
        options.AccessKey = options.AccessKey.Trim();
        options.SecretKey = options.SecretKey.Trim();
        options.Bucket = options.Bucket.Trim();
        options.PublicUrlBase = string.IsNullOrWhiteSpace(options.PublicUrlBase) ? null : options.PublicUrlBase.Trim().TrimEnd('/');

        missing = new (string Key, string Value)[]
            {
                (AccountIdKey, options.AccountId),
                (AccessKeyKey, options.AccessKey),
                (SecretKeyKey, options.SecretKey),
                (BucketKey, options.Bucket),
            }
            .Where(x => x.Value.Length == 0)
            .Select(x => x.Key)
            .ToList();

        return missing.Count == 0 ? options : null;
    }
}
