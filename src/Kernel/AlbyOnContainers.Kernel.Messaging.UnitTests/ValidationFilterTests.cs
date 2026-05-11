namespace AlbyOnContainers.Kernel.Messaging.UnitTests;

using AlbyOnContainers.Kernel.Messaging.Filters;
using FluentValidation;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ValidationResult = FluentValidation.Results.ValidationResult;
using ValidationFailure = FluentValidation.Results.ValidationFailure;

[TestFixture]
public sealed class ValidationFilterTests
{
    private static (ConsumeContext<TestMessage> ctx, IPipe<ConsumeContext<TestMessage>> pipe, int[] downstreamCalls) FakePipe(TestMessage message)
    {
        var ctx = Substitute.For<ConsumeContext<TestMessage>>();
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(CancellationToken.None);

        var pipe = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();
        var counter = new[] { 0 };
        pipe.When(p => p.Send(Arg.Any<ConsumeContext<TestMessage>>()))
            .Do(_ => counter[0]++);

        return (ctx, pipe, counter);
    }

    [Test]
    public async Task Send_WhenNoValidatorRegistered_PassesThroughToNext()
    {
        var filter = new ValidationFilter<TestMessage>(NullLogger<ValidationFilter<TestMessage>>.Instance, validator: null);
        var (ctx, pipe, calls) = FakePipe(new TestMessage("ok"));

        await filter.Send(ctx, pipe);

        Assert.That(calls[0], Is.EqualTo(1));
    }

    [Test]
    public async Task Send_WhenValidatorPasses_PassesThroughToNext()
    {
        var validator = Substitute.For<IValidator<TestMessage>>();
        validator.ValidateAsync(Arg.Any<TestMessage>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var filter = new ValidationFilter<TestMessage>(NullLogger<ValidationFilter<TestMessage>>.Instance, validator);
        var (ctx, pipe, calls) = FakePipe(new TestMessage("ok"));

        await filter.Send(ctx, pipe);

        Assert.That(calls[0], Is.EqualTo(1));
    }

    [Test]
    public void Send_WhenValidatorFails_ThrowsValidationExceptionAndShortCircuits()
    {
        var validator = Substitute.For<IValidator<TestMessage>>();
        validator.ValidateAsync(Arg.Any<TestMessage>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Value", "must not be empty") }));

        var filter = new ValidationFilter<TestMessage>(NullLogger<ValidationFilter<TestMessage>>.Instance, validator);
        var (ctx, pipe, calls) = FakePipe(new TestMessage(""));

        Assert.ThrowsAsync<ValidationException>(() => filter.Send(ctx, pipe));
        Assert.That(calls[0], Is.EqualTo(0), "Pipeline must short-circuit before invoking next.");
    }
}
