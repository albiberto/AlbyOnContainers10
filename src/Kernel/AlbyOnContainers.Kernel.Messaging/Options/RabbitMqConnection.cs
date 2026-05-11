namespace AlbyOnContainers.Kernel.Messaging.Options;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

internal sealed record RabbitMqConnection(
    string Host,
    int Port,
    string VirtualHost,
    string Username,
    string Password,
    bool UseSsl)
{
    public static RabbitMqConnection Resolve(IServiceProvider serviceProvider, MessagingOptions options)
    {
        var connectionString = options.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(options.ConnectionStringName))
        {
            connectionString = serviceProvider
                .GetRequiredService<IConfiguration>()
                .GetConnectionString(options.ConnectionStringName);
        }

        if (!string.IsNullOrWhiteSpace(connectionString))
            return Parse(connectionString);

        return new(
            options.Host ?? throw CreateValidationException("Host is required when no RabbitMQ connection string is configured."),
            options.Port,
            "/",
            options.Username ?? throw CreateValidationException("Username is required when no RabbitMQ connection string is configured."),
            options.Password ?? throw CreateValidationException("Password is required when no RabbitMQ connection string is configured."),
            options.UseSsl);
    }

    public static RabbitMqConnection Parse(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
            throw CreateValidationException("RabbitMQ connection string must be an absolute amqp:// or amqps:// URI.");

        var useSsl = uri.Scheme.Equals("amqps", StringComparison.OrdinalIgnoreCase);
        if (!useSsl && !uri.Scheme.Equals("amqp", StringComparison.OrdinalIgnoreCase))
            throw CreateValidationException("RabbitMQ connection string must use the amqp or amqps scheme.");

        if (string.IsNullOrWhiteSpace(uri.Host))
            throw CreateValidationException("RabbitMQ connection string must include a host.");

        var userInfo = uri.UserInfo.Split(':', 2);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw CreateValidationException("RabbitMQ connection string must include username and password.");

        var virtualHost = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(virtualHost)) virtualHost = "/";

        return new(
            uri.Host,
            uri.IsDefaultPort ? useSsl ? 5671 : 5672 : uri.Port,
            virtualHost,
            username,
            password,
            useSsl);
    }

    private static OptionsValidationException CreateValidationException(string message) =>
        new(Options.DefaultName, typeof(MessagingOptions), [message]);
}
