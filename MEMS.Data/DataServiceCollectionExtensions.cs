using MEMS.Data.Entities;
using MEMS.Data.Services;
using Microsoft.AspNetCore.Identity;
using MEMS.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MEMS.Data;

public static class DataServiceCollectionExtensions
{
    // Registers MEMS.Data against the Aspire-managed SQL Server connection ("memsdb").
    public static IHostApplicationBuilder AddMemsData(this IHostApplicationBuilder builder, string connectionName = "memsdb")
    {
        var connectionString = builder.Configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"Connection string '{connectionName}' was not found. Run the app through MEMS.AppHost.");

        builder.Services.AddMemsData(options => options.UseSqlServer(connectionString));

        // Aspire: health check, OpenTelemetry tracing/metrics and connection retries for EF Core.
        builder.EnrichSqlServerDbContext<MemsDbContext>();
        return builder;
    }

    // Provider-agnostic registration (tests use the in-memory provider).
    //
    // Lifetimes - why the factory is SCOPED:
    // The DbContext takes ITenantContext in its constructor. ITenantContext is scoped (one per HTTP
    // request or Blazor circuit). EF's default factory is a singleton, which would build every context
    // from the root provider - the tenant would be missing (or, if TenantContext were a singleton,
    // shared between users). Registering the factory as scoped makes each context it creates receive
    // the ITenantContext of the scope that owns the factory, i.e. of the current user only. With
    // ValidateScopes on (Development), accidentally resolving it from the root fails fast.
    public static IServiceCollection AddMemsData(this IServiceCollection services, Action<DbContextOptionsBuilder> configureDb)
    {
        services.TryAddScoped<TenantContext>();
        services.TryAddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        services.AddDbContextFactory<MemsDbContext>(configureDb, ServiceLifetime.Scoped);
        // Identity's stores need a context instance; it comes from the same scoped factory.
        services.TryAddScoped(sp => sp.GetRequiredService<IDbContextFactory<MemsDbContext>>().CreateDbContext());

        services.AddScoped<MunicipalityService>();
        services.AddScoped<PositionService>();
        services.AddScoped<CandidateService>();
        services.AddScoped<ImageService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<AdminUserService>();
        return services;
    }

    // Identity configuration shared by the Admin app (cookies) and the API (JWT login).
    public static IdentityBuilder AddMemsIdentityCore(this IServiceCollection services) =>
        services
            .AddDataProtection().Services // password-reset tokens (the API has no cookie auth to add it)
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = Shared.Validation.FieldLengths.MinPasswordLength;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<MemsDbContext>()
            .AddClaimsPrincipalFactory<MemsUserClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();
}
