using AlbyOnContainers.Kernel.Observability.Options;
using OpenTelemetry.Resources;

namespace AlbyOnContainers.Kernel.Observability.Detectors;

internal sealed class OptionsResourceDetector(ObservabilityOptions options) : IResourceDetector
{
    public Resource Detect() =>
        ResourceBuilder.CreateEmpty()
            .AddService(
                serviceName: options.ServiceName,
                serviceNamespace: options.Namespace,
                serviceInstanceId: Environment.MachineName)
            .Build();
}