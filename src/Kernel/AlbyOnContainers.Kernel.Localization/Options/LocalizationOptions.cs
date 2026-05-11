using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AlbyOnContainers.Kernel.Options;

namespace AlbyOnContainers.Kernel.Localization.Options;

public sealed record LocalizationOptions : KernelOptions<LocalizationOptions>, IValidatableObject
{
    [Required(AllowEmptyStrings = false)]
    public string DefaultCulture { get; set; } = "en";

    [Required]
    [MinLength(1, ErrorMessage = "At least one supported culture must be defined.")]
    public string[] SupportedCultures { get; set; } = ["it", "en"];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // 1. Every supported culture must be a valid BCP-47 / CLR culture name.
        //    Catch the misconfiguration at boot rather than at the first request.
        foreach (var culture in SupportedCultures ?? [])
        {
            if (!IsValidCulture(culture))
                yield return new($"Supported culture '{culture}' is not a valid culture name.",[nameof(SupportedCultures)]);
        }

        // 2. DefaultCulture must itself be a valid culture and listed among SupportedCultures,
        //    otherwise the request pipeline silently falls back and serves inconsistent content.
        if (string.IsNullOrWhiteSpace(DefaultCulture)) yield break;
        
        if (!IsValidCulture(DefaultCulture))
            yield return new($"DefaultCulture '{DefaultCulture}' is not a valid culture name.",[nameof(DefaultCulture)]);
        else if (SupportedCultures is { Length: > 0 } && !SupportedCultures.Contains(DefaultCulture, StringComparer.OrdinalIgnoreCase))
            yield return new($"DefaultCulture '{DefaultCulture}' must be one of the SupportedCultures ({string.Join(", ", SupportedCultures)}).",[nameof(DefaultCulture)]);
    }

    private static bool IsValidCulture(string name)
    {
        try
        {
            _ = CultureInfo.GetCultureInfo(name);
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
