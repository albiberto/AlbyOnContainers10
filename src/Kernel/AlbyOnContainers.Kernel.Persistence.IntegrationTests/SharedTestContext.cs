using NUnit.Framework;
using Testcontainers.PostgreSql;

namespace AlbyOnContainers.Kernel.Persistence.IntegrationTests;

[SetUpFixture]
public class SharedTestContext
{
    public static PostgreSqlContainer PostgresContainer { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task InitializeAsync()
    {
        PostgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await PostgresContainer.StartAsync();
    }

    [OneTimeTearDown]
    public async Task DisposeAsync() => await PostgresContainer.DisposeAsync();
}
