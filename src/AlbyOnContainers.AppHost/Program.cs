var builder = DistributedApplication.CreateBuilder(args);

var pgPassword = builder.AddParameter("postgres-password");

// PostgreSQL con volume persistente e password statica
var postgres = builder.AddPostgres("postgres", pgPassword)
    .WithDataVolume("productdatamanager-pgdata")
    .WithPgAdmin()
    .AddDatabase("productdb");

// Keycloak (container generico con realm auto-import)
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "24.0")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithBindMount("../keycloak", "/opt/keycloak/data/import")
    .WithArgs("start-dev", "--import-realm");

// Product Data Manager Web
builder.AddProject<Projects.ProductInformationManager_Web>("productdatamanager")
    .WithReference(postgres)
    .WithExternalHttpEndpoints();

builder.Build().Run();
