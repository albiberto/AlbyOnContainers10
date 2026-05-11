namespace AlbyOnContainers.Kernel.Messaging.UnitTests;

using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Messaging;
using AlbyOnContainers.Kernel.Messaging.Filters;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[TestFixture]
public sealed class MessagingExtensionsTests
{
    // A no-op consume filter used to verify the registry & DI wiring without spinning up MassTransit.
    private sealed class FakeConsumeFilter<T> : IFilter<ConsumeContext<T>> where T : class
    {
        public Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next) => next.Send(context);
        public void Probe(ProbeContext context) => context.CreateFilterScope("fake-filter");
    }

    private static HostApplicationBuilder NewBuilder() => Host.CreateApplicationBuilder();

    [Test]
    public void AddMessagingFilter_WithClosedGenericType_Throws()
    {
        var builder = NewBuilder();
        var kernel = builder.AddKernel();

        var ex = Assert.Throws<ArgumentException>(() => kernel.AddMessagingFilter(typeof(FakeConsumeFilter<string>)));

        Assert.That(ex!.Message, Does.Contain("open generic"));
    }

    [Test]
    public void AddMessagingFilter_WithNullType_Throws()
    {
        var builder = NewBuilder();
        var kernel = builder.AddKernel();

        Assert.Throws<ArgumentNullException>(() => kernel.AddMessagingFilter(null!));
    }

    [Test]
    public void AddMessagingFilter_RegistersFilterTypeInRegistryAndDI()
    {
        var builder = NewBuilder();

        builder.AddKernel().AddMessagingFilter(typeof(FakeConsumeFilter<>));

        using var host = builder.Build();
        var registry = host.Services.GetRequiredService<MessagingFilterRegistry>();
        var openGenericRegistered = host.Services.GetService(typeof(FakeConsumeFilter<string>));

        Assert.Multiple(() =>
        {
            Assert.That(registry.Filters, Has.Count.EqualTo(1));
            Assert.That(registry.Filters[0], Is.EqualTo(typeof(FakeConsumeFilter<>)));
            Assert.That(openGenericRegistered, Is.Not.Null,
                "Open-generic filter must be resolvable as Scoped per closed type at consume time.");
        });
    }

    [Test]
    public void AddMessagingFilter_CalledMultipleTimes_AccumulatesEntries()
    {
        var builder = NewBuilder();

        builder.AddKernel()
            .AddMessagingFilter(typeof(FakeConsumeFilter<>))
            .AddMessagingFilter(typeof(FakeConsumeFilter<>));

        using var host = builder.Build();
        var registry = host.Services.GetRequiredService<MessagingFilterRegistry>();

        Assert.That(registry.Filters, Has.Count.EqualTo(2));
    }
}
