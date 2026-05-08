namespace AlbyOnContainers.Kernel.Caching.UnitTests;

using Cache;
using Caching.Abstractions;

[TestFixture]
public sealed class KeyTests
{
    private sealed class Sample;

    [Test]
    public void Type_WithoutId_ReturnsTypeName()
    {
        var key = Key.Type<Sample>();

        Assert.Multiple(() =>
        {
            Assert.That(key.Value, Is.EqualTo(nameof(Sample)));
            Assert.That(key, Is.AssignableTo<IKey>());
        });
    }

    [Test]
    public void Type_WithId_AppendsIdSegment()
    {
        var key = Key.Type<Sample>("42");

        Assert.That(key.Value, Is.EqualTo("Sample:42"));
    }

    [Test]
    public void Type_WithWhitespaceId_FallsBackToTypeName()
    {
        var key = Key.Type<Sample>("   ");

        Assert.That(key.Value, Is.EqualTo("Sample"));
    }

    [Test]
    public void User_FormatsAsUserSegment()
    {
        var key = Key.User("alice@example.com");

        Assert.That(key.Value, Is.EqualTo("User:alice@example.com"));
    }

    [Test]
    public void User_WithBlankIdentifier_Throws() =>
        Assert.Throws<ArgumentException>(() => Key.User("   "));

    [Test]
    public void Custom_PreservesProvidedKey()
    {
        var key = Key.Custom("legacy-key");

        Assert.That(key.Value, Is.EqualTo("legacy-key"));
    }

    [Test]
    public void WithUser_AppendsUserSegment()
    {
        var key = Key.Type<Sample>("42").WithUser("alice");

        Assert.That(key.Value, Is.EqualTo("Sample:42:User:alice"));
    }

    [Test]
    public void WithCustom_AppendsCustomSegment()
    {
        var key = Key.Type<Sample>().WithCustom("variant-A");

        Assert.That(key.Value, Is.EqualTo("Sample:variant-A"));
    }

    [Test]
    public void ImplicitConversion_To_String_ReturnsValue()
    {
        string asString = Key.Custom("plain");

        Assert.That(asString, Is.EqualTo("plain"));
    }

    [Test]
    public void ToString_ReturnsValue()
    {
        var key = Key.Type<Sample>("99");

        Assert.That(key.ToString(), Is.EqualTo("Sample:99"));
    }
}
