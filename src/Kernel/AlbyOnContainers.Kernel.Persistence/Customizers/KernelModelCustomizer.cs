using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AlbyOnContainers.Kernel.Persistence.Customizers;

public class KernelModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        // 1. Executes the standard and microservice-specific configurations
        base.Customize(modelBuilder, context);

        // 2. Resolves all plugins registered in the application
        var options = context.GetService<IDbContextOptions>();
        var extension = options.FindExtension<KernelModelPluginsExtension>();

        if (extension is null) return;
        
        foreach (var plugin in extension.Plugins) plugin.Apply(modelBuilder);
    }
}