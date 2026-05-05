namespace AlbyOnContainers.Kernel.Persistence.UnitTests.Options;

using AlbyOnContainers.Kernel.Persistence.Options;

public sealed class PersistenceOptionsTests
{
    [Fact]
    public void Default_values_are_production_safe()
    {
        var options = new PersistenceOptions();

        Assert.False(options.EnableSensitiveDataLogging);
        Assert.False(options.EnableDetailedErrors);
        Assert.Equal(500, options.SlowQueryThresholdMs);
        Assert.Equal(string.Empty, options.MetricPrefix);
        Assert.False(options.RunMigrationsOnStartup);
    }
}
