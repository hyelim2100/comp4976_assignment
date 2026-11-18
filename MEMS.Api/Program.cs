using MEMS.Data;
using MEMS.Data.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// EF Core + SQL Server ("memsdb" connection string from the Aspire AppHost) and the service layer.
builder.AddMemsData();
builder.Services.AddMemsIdentityCore();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// The API owns the database lifecycle: migrations + seed data run once here at startup.
// (The Admin app waits for the API in the AppHost, so it always starts with an up-to-date schema.)
using (var scope = app.Services.CreateScope())
{
    await MemsSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
