using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Options;

namespace AlbyOnContainers.Kernel.Messaging.Options;

public class MessagingOptions : KernelOptions<MessagingOptions>
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
