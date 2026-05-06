// File: src/Kernel/AlbyOnContainers.Kernel.Persistence/Customizers/KernelModelPluginsExtension.cs

using AlbyOnContainers.Kernel.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace AlbyOnContainers.Kernel.Persistence.Customizers;

public class KernelModelPluginsExtension(IEnumerable<IModelConfigurationPlugin> plugins) : IDbContextOptionsExtension
{
    public IEnumerable<IModelConfigurationPlugin> Plugins { get; } = plugins;

    public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services) { } 

    public void Validate(IDbContextOptions options) { }

    private sealed class ExtensionInfo(KernelModelPluginsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;
        public override string LogFragment => "KernelModelPlugins";

        public override int GetServiceProviderHashCode() => extension.Plugins.Aggregate(0, (h, p) => HashCode.Combine(h, p.GetType()));

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo ext && extension.Plugins.Select(p => p.GetType()).SequenceEqual(ext.Extension.Plugins.Select(p => p.GetType()));
            
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) { }
        
        public override KernelModelPluginsExtension Extension => (KernelModelPluginsExtension)base.Extension;
    }
}