var builder = DistributedApplication.CreateBuilder(args);

// SQL Server runs in a Docker container; the data volume keeps the database between runs.
var sql = builder.AddSqlServer("sql")
    .WithDataVolume("mems-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("memsdb");

// JWT signing key for the API. Generated on first run and persisted in the AppHost user-secrets
// (Parameters:jwt-key), so it is never committed to the repository.
var jwtKey = builder.AddParameter("jwt-key", new GenerateParameterDefault { MinLength = 64, Special = false }, secret: true, persist: true);

// The API applies migrations and seeds the database, so the Admin app waits for it.
var api = builder.AddProject<Projects.MEMS_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithHttpHealthCheck("/health")
    .WithUrlForEndpoint("https", url => { url.Url += "/swagger"; url.DisplayText = "Swagger UI"; });

builder.AddProject<Projects.MEMS_Admin>("admin")
    .WithReference(db)
    .WaitFor(api)
    .WithHttpHealthCheck("/health")
    .WithUrlForEndpoint("https", url => { url.Url += "/admin"; url.DisplayText = "Admin site"; });

// The public Blazor WebAssembly site never talks to the database: its host proxies /api/* to the API
// through service discovery.
builder.AddProject<Projects.MEMS_Public>("public")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpHealthCheck("/health")
    .WithUrlForEndpoint("https", url => url.DisplayText = "Public site");

builder.Build().Run();
