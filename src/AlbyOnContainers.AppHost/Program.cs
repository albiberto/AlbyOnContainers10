var builder = DistributedApplication.CreateBuilder(args);

var pgPassword = builder.AddParameter("postgres-password");

// PostgreSQL con volume persistente e password statica
var postgres = builder
    .AddPostgres("postgres", pgPassword)
    .WithDataVolume("productdatamanager-pgdata")
    .WithPgAdmin()
    .AddDatabase("productdb");

// RabbitMQ con Management UI
var rmqUsername = builder.AddParameter("rabbitmq-username");
var rmqPassword = builder.AddParameter("rabbitmq-password");

var messaging = builder.AddRabbitMQ("messaging", rmqUsername, rmqPassword)
    .WithManagementPlugin();

// Redis per FusionCache backplane
var redis = builder.AddRedis("cache")
    .WithDataVolume("productdatamanager-redisdata");

// Keycloak (container generico con realm auto-import)
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "24.0")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithBindMount("../keycloak", "/opt/keycloak/data/import")
    .WithArgs("start-dev", "--import-realm");

// ══════════════════════════════════════════
// Observability Stack (LGTM + OTel Collector)
// ══════════════════════════════════════════

// Tempo — Trace backend (OTLP/gRPC :4317 interno, HTTP :3200)
var tempo = builder.AddContainer("tempo", "grafana/tempo", "latest")
    .WithBindMount("./observability/tempo-config.yaml", "/etc/tempo.yaml")
    .WithArgs("-config.file=/etc/tempo.yaml")
    .WithHttpEndpoint(port: 3200, targetPort: 3200, name: "http")
    .WithEndpoint(port: 4317, targetPort: 4317, name: "grpc");

// Loki — Log backend (HTTP :3100, riceve OTLP push dal Collector)
var loki = builder.AddContainer("loki", "grafana/loki", "latest")
    .WithHttpEndpoint(port: 3100, targetPort: 3100, name: "http")
    .WithBindMount("./observability/loki-config.yaml", "/etc/loki/local-config.yaml");

// Prometheus — Metrics backend (HTTP :9090, riceve Remote Write dal Collector)
var prometheus = builder.AddContainer("prometheus", "prom/prometheus", "latest")
    .WithHttpEndpoint(port: 9090, targetPort: 9090, name: "http")
    .WithBindMount("./observability/prometheus.yml", "/etc/prometheus/prometheus.yml")
    .WithArgs(
        "--config.file=/etc/prometheus/prometheus.yml",
        "--storage.tsdb.path=/prometheus",
        "--web.enable-remote-write-receiver");

// OTel Collector — Fan-out gateway (riceve OTLP dall'app, smista ai 3 backend)
var collector = builder.AddContainer("otel-collector", "otel/opentelemetry-collector-contrib", "latest")
    .WithHttpEndpoint(port: 4317, targetPort: 4317, name: "grpc")
    .WithHttpEndpoint(port: 4318, targetPort: 4318, name: "http")
    .WithBindMount("./observability/otel-collector-config.yaml", "/etc/otelcol-contrib/config.yaml");

// Grafana — Visualization (pre-provisioned con 3 datasource)
builder.AddContainer("grafana", "grafana/grafana", "latest")
    .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "http")
    .WithBindMount("./observability/grafana-datasources.yml",
        "/etc/grafana/provisioning/datasources/datasources.yml")
    .WithEnvironment("GF_SECURITY_ADMIN_USER", "admin")
    .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Admin");

// Product Data Manager Web
builder.AddProject<Projects.ProductInformationManager_Web>("productdatamanager")
    .WithReference(postgres)
    .WithReference(messaging)
    .WithReference(redis)
    .WaitFor(postgres)
    .WaitFor(messaging)
    .WaitFor(redis)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", collector.GetEndpoint("grpc"))
    .WithExternalHttpEndpoints();

builder.Build().Run();
