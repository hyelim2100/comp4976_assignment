using MEMS.Api.Infrastructure;
using MEMS.Data;
using MEMS.Data.Entities;
using MEMS.Data.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// EF Core + SQL Server ("memsdb" connection string from the Aspire AppHost) and the service layer.
builder.AddMemsData();
builder.Services.AddMemsIdentityCore().AddSignInManager();

// ---- Authentication: JWT bearer tokens issued by POST /api/auth/login ----
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.Key) >= 32,
        "Jwt:Key is missing or shorter than 32 bytes. Run the API through MEMS.AppHost, which supplies it.")
    .ValidateOnStart();
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwt) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = jwt.Value.GetSigningKey(),
            NameClaimType = JwtClaimNames.Name,
            RoleClaimType = JwtClaimNames.Role,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        options.Events = new JwtBearerEvents
        {
            // Reject tokens of deleted administrators or of accounts whose role/tenant/password changed
            // since the token was issued (the security stamp changes in those cases).
            OnTokenValidated = async context =>
            {
                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var userId = context.Principal?.FindFirst("sub")?.Value;
                var user = userId is null ? null : await userManager.FindByIdAsync(userId);
                if (user is null || user.SecurityStamp != context.Principal!.FindFirst(JwtClaimNames.SecurityStamp)?.Value)
                {
                    context.Fail("The token is no longer valid for this account.");
                }
            },
        };
    });
builder.Services.AddAuthorization();

// ---- Errors: RFC 7807 ProblemDetails everywhere, no exception details to clients ----
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---- MVC: the tenant filter runs for every action ----
builder.Services.AddScoped<TenantResolutionFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<TenantResolutionFilter>());

// ---- OpenAPI document (/openapi/v1.json) + Swagger UI (/swagger) ----
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "MEMS API",
            Version = "v1",
            Description = """
                Municipal Elections Management System - multi-tenant Web API.

                * Tenant routes are `/api/{tenant}/...` where `{tenant}` is the municipality slug (case-insensitive).
                  Every response contains only that tenant's data; an unknown slug returns 404.
                * GET endpoints are public. POST / PUT / DELETE need `Authorization: Bearer {token}` from `POST /api/auth/login`.
                * A Municipality Admin token used on another tenant's route returns 403; an id of another tenant returns 404.
                * Errors use RFC 7807 `application/problem+json` (validation errors: `ValidationProblemDetails`).
                * Voting-guide selections are never sent to this API (they stay in the browser's sessionStorage).
                """,
        };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Token from POST /api/auth/login (e.g. user burnabyadmin / P@$$w0rd).",
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [],
            });
        }
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// The API owns the database lifecycle: migrations + seed data run once here at startup.
// (The Admin app waits for the API in the AppHost, so it always starts with an up-to-date schema.)
using (var scope = app.Services.CreateScope())
{
    await MemsSeeder.SeedAsync(scope.ServiceProvider);
}

app.UseExceptionHandler();
app.UseStatusCodePages(); // empty 401/403/404/405 responses get a ProblemDetails body

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "MEMS API v1");
    options.DocumentTitle = "MEMS API";
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
