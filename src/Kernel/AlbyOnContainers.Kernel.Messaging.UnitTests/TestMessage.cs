namespace AlbyOnContainers.Kernel.Messaging.UnitTests;

// Top-level public record so Castle.DynamicProxy can proxy ConsumeContext<TestMessage> from NSubstitute.
public sealed record TestMessage(string Value);
