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

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;
        public override string LogFragment => "KernelModelPlugins";
        public override int GetServiceProviderHashCode() => 0;
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => true;
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) { }
    }
}