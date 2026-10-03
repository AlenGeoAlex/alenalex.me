using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Options;

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>
    /// Salt for hashing visitor IPs. Changing it detaches every stored like and pending entry
    /// from its visitor.
    /// </summary>
    [Required]
    public string HashingSalt { get; set; } = "";

    /// <summary>
    /// Header carrying the real client IP behind a proxy, e.g. <c>CF-Connecting-IP</c>.
    /// Only set it when the proxy always overwrites the header, otherwise clients can spoof it.
    /// </summary>
    public string? IpHeader { get; set; }

    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>Serve the OpenAPI document and the Scalar UI at <c>/scalar</c>.</summary>
    public bool EnableScalar { get; set; }
}

[OptionsValidator]
public sealed partial class ApiOptionsValidator : IValidateOptions<ApiOptions>;
