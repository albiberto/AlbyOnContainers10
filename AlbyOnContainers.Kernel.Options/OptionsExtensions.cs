// ReSharper disable once CheckNamespace

namespace Microsoft.Extensions.DependencyInjection;

using AlbyOnContainers.Kernel.Options;

public static class OptionsExtensions
{
    extension(IServiceCollection services)
    {
        public void BindOptions<TOptions>(string? section) where TOptions : KernelOptions<TOptions> =>
            services.AddOptions<TOptions>()
                .BindConfiguration(section ?? KernelOptions<TOptions>.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        public void BindKeyedOptions<TOptions>(string key, string? section) where TOptions : KernelOptions<TOptions> =>
            services.AddOptions<TOptions>(key)
                .BindConfiguration(section ?? KernelOptions<TOptions>.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        public void ConfigureOptions<TOptions>(Action<TOptions> configureOptions) where TOptions : KernelOptions<TOptions> =>
            services.AddOptions<TOptions>()
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        public void ConfigureKeyedOptions<TOptions>(string key, Action<TOptions> configureOptions) where TOptions : KernelOptions<TOptions> =>
            services.AddOptions<TOptions>(key)
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();
    }
}