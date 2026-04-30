using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AlbyOnContainers.Kernel;

/// <summary>
/// Fluent entry point for composing the AlbyOnContainers kernel.
/// Each kernel module (Caching, Messaging, Persistence, ...) registers itself
/// via an extension method on <see cref="IKernelBuilder"/>.
/// </summary>
public interface IKernelBuilder
{
    /// <summary>Underlying ASP.NET Core / Generic host application builder.</summary>
    IHostApplicationBuilder Host { get; }

    /// <summary>Shortcut for <c>Host.Services</c>.</summary>
    IServiceCollection Services => Host.Services;

    /// <summary>Shortcut for <c>Host.Configuration</c>.</summary>
    IConfiguration Configuration => Host.Configuration;
}