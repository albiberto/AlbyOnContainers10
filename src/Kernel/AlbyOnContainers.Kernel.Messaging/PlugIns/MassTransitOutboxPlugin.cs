namespace AlbyOnContainers.Kernel.Messaging.PlugIns;

using Persistence.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;

internal sealed class MassTransitOutboxPlugin : IModelConfigurationPlugin
{
    public void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}