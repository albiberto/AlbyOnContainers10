namespace AlbyOnContainers.Kernel.Caching.IntegrationTests;

using Testcontainers.Redis;

[SetUpFixture]
public class SharedTestContext
{
    public static RedisContainer RedisContainer { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task InitializeAsync()
    {
        RedisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await RedisContainer.StartAsync();
    }

    [OneTimeTearDown]
    public async Task DisposeAsync() => await RedisContainer.DisposeAsync();
}
