using System.ComponentModel.DataAnnotations;

namespace AlbyOnContainers.Kernel.Messaging.Options;

public class MessagingOptions
{
    public const string SectionName = "Messaging";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public bool EnableOutbox { get; set; } = true;
    public bool EnableMediator { get; set; } = true;
}
