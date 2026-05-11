namespace AlbyOnContainers.Kernel.Messaging.UnitTests;

using AlbyOnContainers.Kernel.Messaging.Filters;

[TestFixture]
public sealed class MessagingFilterRegistryTests
{
    private sealed class FakeFilter<T> { }

    [Test]
    public void Filters_StartsEmpty()
    {
        var registry = new MessagingFilterRegistry();

        Assert.That(registry.Filters, Is.Empty);
    }

    [Test]
    public void Add_AppendsTypeAndPreservesInsertionOrder()
    {
        var registry = new MessagingFilterRegistry();

        registry.Add(typeof(FakeFilter<>));
        registry.Add(typeof(IList<>));

        Assert.Multiple(() =>
        {
            Assert.That(registry.Filters, Has.Count.EqualTo(2));
            Assert.That(registry.Filters[0], Is.EqualTo(typeof(FakeFilter<>)));
            Assert.That(registry.Filters[1], Is.EqualTo(typeof(IList<>)));
        });
    }
}
