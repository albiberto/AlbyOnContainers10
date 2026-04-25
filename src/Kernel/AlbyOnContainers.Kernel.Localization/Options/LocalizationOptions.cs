using System.ComponentModel.DataAnnotations;

namespace AlbyOnContainers.Kernel.Localization.Options;

public class LocalizationOptions
{
    public const string SectionName = "Localization";

    [Required]
    public string ResourcesPath { get; set; } = "Resources";

    [Required]
    public string DefaultCulture { get; set; } = "it";

    [Required]
    [MinLength(1)]
    public string[] SupportedCultures { get; set; } = ["en", "it"];
}
