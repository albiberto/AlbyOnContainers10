namespace AlbyOnContainers.Kernel.Messaging.UnitTests;

using AlbyOnContainers.Kernel.Domain.Exceptions;
using AlbyOnContainers.Kernel.Messaging.Filters;
using FluentValidation;
using FluentValidation.Results;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

[TestFixture]
public sealed class GlobalExceptionFilterTests
{
    private static GlobalExceptionFilter<TestMessage> NewFilter() =>
        new(NullLogger<GlobalExceptionFilter<TestMessage>>.Instance);

    private static (ConsumeContext<TestMessage>, IPipe<ConsumeContext<TestMessage>>) Pipe(Action<ConsumeContext<TestMessage>> action)
    {
        var context = Substitute.For<ConsumeContext<TestMessage>>();
        var pipe = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();
        pipe.When(p => p.Send(Arg.Any<ConsumeContext<TestMessage>>()))
            .Do(call => action(call.Arg<ConsumeContext<TestMessage>>()));
        return (context, pipe);
    }

    [Test]
    public void Send_WhenNextSucceeds_DoesNotThrow()
    {
        var filter = NewFilter();
        var (context, pipe) = Pipe(_ => { });

        Assert.DoesNotThrowAsync(() => filter.Send(context, pipe));
    }

    [Test]
    public void Send_WhenNextThrowsValidationException_RethrowsWithoutWrapping()
    {
        var filter = NewFilter();
        var thrown = new ValidationException(new[] { new ValidationFailure("Field", "boom") });
        var (context, pipe) = Pipe(_ => throw thrown);

        var ex = Assert.ThrowsAsync<ValidationException>(() => filter.Send(context, pipe));
        Assert.That(ex, Is.SameAs(thrown));
    }

    [Test]
    public void Send_WhenNextThrowsDomainException_RethrowsWithoutWrapping()
    {
        var filter = NewFilter();
        var thrown = new DomainException("rule violated");
        var (context, pipe) = Pipe(_ => throw thrown);

        var ex = Assert.ThrowsAsync<DomainException>(() => filter.Send(context, pipe));
        Assert.That(ex, Is.SameAs(thrown));
    }

    [Test]
    public void Send_WhenNextThrowsOperationCanceled_RethrowsWithoutLogging()
    {
        var filter = NewFilter();
        var (context, pipe) = Pipe(_ => throw new OperationCanceledException("host shutdown"));

        Assert.ThrowsAsync<OperationCanceledException>(() => filter.Send(context, pipe));
    }

    [Test]
    public void Send_WhenNextThrowsGenericException_RethrowsWithoutWrapping()
    {
        var filter = NewFilter();
        var thrown = new InvalidOperationException("boom");
        var (context, pipe) = Pipe(_ => throw thrown);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => filter.Send(context, pipe));
        Assert.That(ex, Is.SameAs(thrown));
    }

    [Test]
    public void Probe_RegistersDistinctFilterScope()
    {
        var filter = NewFilter();
        var probe = Substitute.For<ProbeContext>();

        filter.Probe(probe);

        probe.Received(1).CreateFilterScope("global-exception-logger");
    }
}
