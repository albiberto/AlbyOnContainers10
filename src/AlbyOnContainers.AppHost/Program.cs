var builder = DistributedApplication.CreateBuilder(args);

var pgPassword = builder.AddParameter("postgres-password");
var postgresServer = builder
    .AddPostgres("postgres", pgPassword)
    .WithDataVolume("productdatamanager-pgdata")
    .WithPgAdmin();

var productDb = postgresServer.AddDatabase("productdb");

var rmqUsername = builder.AddParameter("rabbitmq-username");
var rmqPassword = builder.AddParameter("rabbitmq-password");

var messaging = builder
    .AddRabbitMQ("messaging", rmqUsername, rmqPassword)
    .WithManagementPlugin();

var cache = builder
    .AddRedis("cache")
    .WithDataVolume("productdatamanager-redisdata");

builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "24.0")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithBindMount("../keycloak", "/opt/keycloak/data/import")
    .WithArgs("start-dev", "--import-realm");

var tempo = builder.AddContainer("tempo", "grafana/tempo", "2.10.4")
    .WithBindMount("./observability/tempo-config.yaml", "/etc/tempo.yaml")
    .WithArgs("-config.file=/etc/tempo.yaml")
    .WithHttpEndpoint(port: 3200, targetPort: 3200, name: "http");

var loki = builder.AddContainer("loki", "grafana/loki", "latest")
    .WithHttpEndpoint(port: 3100, targetPort: 3100, name: "http")
    .WithBindMount("./observability/loki-config.yaml", "/etc/loki/local-config.yaml");

var prometheus = builder.AddContainer("prometheus", "prom/prometheus", "latest")
    .WithHttpEndpoint(port: 9090, targetPort: 9090, name: "http")
    .WithBindMount("./observability/prometheus.yml", "/etc/prometheus/prometheus.yml")
    .WithArgs(
        "--config.file=/etc/prometheus/prometheus.yml",
        "--storage.tsdb.path=/prometheus",
        "--web.enable-remote-write-receiver");

builder.AddContainer("grafana", "grafana/grafana", "latest")
    .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "http")
    .WithBindMount(
        "./observability/grafana-datasources.yml",
        "/etc/grafana/provisioning/datasources/datasources.yml")
    .WithBindMount(
        "./observability/grafana-dashboards.yml",
        "/etc/grafana/provisioning/dashboards/dashboards.yml")
    .WithBindMount("./observability/dashboards", "/var/lib/grafana/dashboards")
    .WithEnvironment("GF_SECURITY_ADMIN_USER", "admin")
    .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Viewer")
    .WithEnvironment("GF_DASHBOARDS_DEFAULT_HOME_DASHBOARD_PATH", "/var/lib/grafana/dashboards/pim-overview.json");

var collector = builder.AddContainer("otel-collector", "otel/opentelemetry-collector-contrib", "latest")
    .WithEndpoint(port: 4317, targetPort: 4317, name: "grpc", scheme: "http")
    .WithHttpEndpoint(port: 4318, targetPort: 4318, name: "http")
    .WithBindMount("./observability/otel-collector-config.yaml", "/etc/otelcol-contrib/config.yaml")
    .WaitFor(tempo)
    .WaitFor(loki)
    .WaitFor(prometheus);

var otelServiceName = builder.Configuration["Otel:ServiceName"] ?? "product-information-manager";
var otelNamespace = builder.Configuration["Otel:Namespace"] ?? "alby-on-containers";
var otelEnvironment = builder.Configuration["Otel:Environment"] ?? "development";

builder.AddProject<Projects.ProductInformationManager_Web>("productdatamanager")
    .WithReference(productDb)
    .WithReference(messaging)
    .WithReference(cache)
    .WaitFor(productDb)
    .WaitFor(messaging)
    .WaitFor(cache)
    .WaitFor(collector)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", collector.GetEndpoint("grpc"))
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc")
    .WithEnvironment("OTEL_SERVICE_NAME", otelServiceName)
    .WithEnvironment(
        "OTEL_RESOURCE_ATTRIBUTES",
        $"service.namespace={otelNamespace},deployment.environment={otelEnvironment},deployment.environment.name={otelEnvironment}")
    .WithExternalHttpEndpoints();

builder.Build().Run();
