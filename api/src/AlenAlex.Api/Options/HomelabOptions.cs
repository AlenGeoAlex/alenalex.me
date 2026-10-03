using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Options;

public sealed class HomelabOptions
{
    public const string SectionName = "Homelab";

    /// <summary>Checked every minute.</summary>
    [ValidateEnumeratedItems]
    public List<HomelabService> Services { get; set; } = [];
}

/// <summary>Any 2xx/3xx within 5 s counts as up.</summary>
public sealed class HomelabService
{
    [Required]
    public string Name { get; set; } = "";

    [Required, Url]
    public string Url { get; set; } = "";
}

[OptionsValidator]
public sealed partial class HomelabOptionsValidator : IValidateOptions<HomelabOptions>;
