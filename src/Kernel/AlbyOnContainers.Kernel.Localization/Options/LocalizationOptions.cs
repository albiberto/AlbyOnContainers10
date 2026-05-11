using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Options;

namespace AlbyOnContainers.Kernel.Localization.Options;

public sealed record LocalizationOptions : KernelOptions<LocalizationOptions>
{
    [Required(AllowEmptyStrings = false)]
    public string DefaultCulture { get; set; } = "en";

    [Required]
    [MinLength(1, ErrorMessage = "At least one supported culture must be defined.")]
    public string[] SupportedCultures { get; set; } = ["it", "en"];
}