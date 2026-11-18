using MEMS.Data.Entities;
using MEMS.Data.Services;
using MEMS.Data.Tenancy;
using MEMS.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MEMS.Data.Seeding;

// Applies migrations and seeds tenants, positions, candidates (with A1 sample photos), roles and the
// administrator accounts. Idempotent: sample election data is added only to an empty database, and
// accounts only when missing - so a tenant deleted by a Super Admin is not re-created on restart.
public static class MemsSeeder
{
    public const string DefaultPassword = "P@$$w0rd";

    private static readonly DateOnly ElectionDate = new(2026, 10, 17);

    // Called with a fresh scope; that scope's tenant is set to Super Admin (no query filters).
    public static async Task SeedAsync(IServiceProvider scopedServices, CancellationToken cancellationToken = default)
    {
        var logger = scopedServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MemsSeeder));
        scopedServices.GetRequiredService<TenantContext>().SetSuperAdmin();

        var dbFactory = scopedServices.GetRequiredService<IDbContextFactory<MemsDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        logger.LogInformation("Applying database migrations");
        await db.Database.MigrateAsync(cancellationToken);

        await SeedRolesAsync(scopedServices.GetRequiredService<RoleManager<IdentityRole>>());

        if (!await db.Municipalities.AnyAsync(cancellationToken))
        {
            await SeedElectionDataAsync(db, cancellationToken);
            logger.LogInformation("Seeded {MunicipalityCount} municipalities, {PositionCount} positions and {CandidateCount} candidates",
                await db.Municipalities.CountAsync(cancellationToken),
                await db.Positions.CountAsync(cancellationToken),
                await db.Candidates.CountAsync(cancellationToken));
        }

        await SeedAccountsAsync(db, scopedServices, logger);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                ThrowIfFailed(await roleManager.CreateAsync(new IdentityRole(role)), $"create role '{role}'");
            }
        }
    }

    // Spec accounts (password P@$$w0rd). "Super" and "super" are the same account: user names are
    // matched case-insensitively by Identity (normalized user names).
    private static readonly (string UserName, string Role, string? TenantSlug)[] Accounts =
    [
        ("super", AppRoles.SuperAdmin, null),
        ("burnabyadmin", AppRoles.MunicipalityAdmin, "burnaby"),
        ("surreyadmin", AppRoles.MunicipalityAdmin, "surrey"),
        ("richmondadmin", AppRoles.MunicipalityAdmin, "richmond"),
        ("pm", AppRoles.MunicipalityAdmin, "pitt-meadows"),
        ("mapleridgeadmin", AppRoles.MunicipalityAdmin, "maple-ridge"),
    ];

    private static async Task SeedAccountsAsync(MemsDbContext db, IServiceProvider services, ILogger logger)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var adminUsers = services.GetRequiredService<AdminUserService>();

        foreach (var (userName, role, slug) in Accounts)
        {
            if (await userManager.FindByNameAsync(userName) is not null)
            {
                continue;
            }

            int? municipalityId = null;
            if (slug is not null)
            {
                municipalityId = await db.Municipalities.Where(m => m.Slug == slug).Select(m => (int?)m.Id).FirstOrDefaultAsync();
                if (municipalityId is null)
                {
                    continue; // tenant was deleted by a Super Admin
                }
            }

            var result = await adminUsers.CreateAccountAsync(userName, $"{userName}@example.com", DefaultPassword, role, municipalityId);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed account '{userName}': {result.Message}");
            }
            logger.LogInformation("Seeded account {UserName} ({Role})", userName, role);
        }
    }

    private static async Task SeedElectionDataAsync(MemsDbContext db, CancellationToken cancellationToken)
    {
        foreach (var seed in Tenants)
        {
            var municipality = new Municipality
            {
                Name = seed.Name,
                Slug = seed.Slug,
                Province = "British Columbia",
                ElectionDate = ElectionDate,
                Tagline = seed.Tagline,
                Description = seed.Description,
                PrimaryColor = seed.Color,
            };
            db.Municipalities.Add(municipality);
            await db.SaveChangesAsync(cancellationToken);

            municipality.LogoImageId = AddImage(db, $"seed-logo-{seed.Slug}.png", municipality.Id);

            foreach (var (positionName, seats, candidates) in seed.Positions)
            {
                var position = new Position { Name = positionName, SeatsAvailable = seats, MunicipalityId = municipality.Id };
                db.Positions.Add(position);
                await db.SaveChangesAsync(cancellationToken);

                foreach (var c in candidates)
                {
                    db.Candidates.Add(new Candidate
                    {
                        Name = c.Name,
                        Profile = c.Profile,
                        Website = c.Website,
                        Email = c.Email,
                        PositionId = position.Id,
                        MunicipalityId = municipality.Id,
                        PhotoImageId = AddImage(db, $"seed-photo-{Shared.Validation.SlugRules.FromName(c.Name)}.png", municipality.Id),
                    });
                }
            }
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    // Seed images are embedded resources (SeedData/Images) - the A1 photos plus logos for each tenant.
    private static Guid? AddImage(MemsDbContext db, string fileName, int municipalityId)
    {
        using var stream = typeof(MemsSeeder).Assembly.GetManifestResourceStream($"SeedImages.{fileName}");
        if (stream is null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var image = new StoredImage { ContentType = "image/png", FileName = fileName, Data = buffer.ToArray(), MunicipalityId = municipalityId };
        db.Images.Add(image);
        return image.Id;
    }

    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to {action}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    private sealed record CandidateSeed(string Name, string Profile, string? Website = null, string? Email = null);

    private sealed record TenantSeed(
        string Name,
        string Slug,
        string Color,
        string Tagline,
        string Description,
        (string Name, int Seats, CandidateSeed[] Candidates)[] Positions);

    // 5 tenants x 3 positions, 22 candidates (the A1 sample candidates and photos, redistributed so that
    // Burnaby, Surrey and Richmond all have a full ballot).
    private static readonly TenantSeed[] Tenants =
    [
        new("Burnaby", "burnaby", "#8E44AD",
            "Burnaby votes - your city, your choice.",
            "A large city between Vancouver and the Fraser Valley, home to Metrotown, Burnaby Mountain and extensive parkland.",
            [
                ("Mayor", 1, [
                    new("Julia Kowalski", "Former city planner running on housing supply and rental protection.", "https://example.com/kowalski", "julia.kowalski@example.com"),
                    new("Hamid Rahimi", "Community organizer focused on transit, childcare and neighbourhood services.", "https://example.com/rahimi"),
                ]),
                ("Councillor", 8, [
                    new("Nina Petrova", "Accountant committed to transparent budgeting and responsible spending.", Email: "nina.petrova@example.com"),
                    new("Aisha Karim", "Climate policy researcher promoting renewable energy and tree canopy growth.", "https://example.com/karim"),
                ]),
                ("School Trustee", 7, [
                    new("Diego Alvarez", "High school counsellor focused on student wellbeing and career pathways.", Email: "diego.alvarez@example.com"),
                ]),
            ]),
        new("Surrey", "surrey", "#0E7C86",
            "Surrey decides - the future lives here.",
            "One of the fastest-growing cities in British Columbia, with town centres from Whalley and Guildford to Cloverdale and South Surrey.",
            [
                ("Mayor", 1, [
                    new("Eleanor Whitfield", "Two-term councillor focused on affordable housing and protecting agricultural land.", "https://example.com/whitfield", "eleanor.whitfield@example.com"),
                ]),
                ("Councillor", 8, [
                    new("Priya Sandhu", "Registered nurse advocating for expanded community health services.", Email: "priya.sandhu@example.com"),
                    new("Daniel Okafor", "Civil engineer with a focus on flood protection and infrastructure renewal.", "https://example.com/okafor"),
                    new("Samuel Wright", "Arts administrator advocating for cultural spaces and local festivals."),
                ]),
                ("School Trustee", 6, [
                    new("Fiona MacLeod", "Parent and librarian campaigning for literacy programs and library funding."),
                ]),
            ]),
        new("Richmond", "richmond", "#C0392B",
            "Richmond at the polls - island city, one voice.",
            "An island city at the mouth of the Fraser River, known for Steveston Village, its dikes and its diverse communities.",
            [
                ("Mayor", 1, [
                    new("Robert Tanaka", "Long-time councillor seeking to complete the city centre plan.", "https://example.com/tanaka", "robert.tanaka@example.com"),
                ]),
                ("Councillor", 8, [
                    new("Kevin Byrne", "Small business advocate focused on reducing red tape for local entrepreneurs.", "https://example.com/byrne"),
                    new("Amara Nwosu", "Social worker campaigning on homelessness prevention and mental health support.", Email: "amara.nwosu@example.com"),
                ]),
                ("School Trustee", 7, [
                    new("Mei Chen", "Software developer pushing for stronger technology and coding programs in schools.", "https://example.com/chen"),
                ]),
            ]),
        new("Pitt Meadows", "pitt-meadows", "#1F6FB2",
            "Pitt Meadows votes - small city, big say.",
            "A small city in Metro Vancouver bordered by the Fraser and Pitt Rivers, known for its farmland, dikes and trails.",
            [
                ("Mayor", 1, [
                    new("Marcus Delgado", "Local business owner campaigning on transit connections and downtown revitalization.", "https://example.com/delgado"),
                ]),
                ("Councillor", 6, [
                    new("Grace Liu", "Youth sports coach and volunteer pushing for more parks and recreation programs."),
                    new("Tom Hargreaves", "Retired firefighter running on public safety and emergency preparedness.", Email: "tom.hargreaves@example.com"),
                ]),
                ("School Trustee", 2, [
                    new("Hannah Moreau", "Former teacher committed to smaller class sizes and inclusive education.", "https://example.com/moreau"),
                    new("Arjun Mehta", "Parent advisory council chair focused on school safety and modern facilities.", Email: "arjun.mehta@example.com"),
                ]),
            ]),
        new("Maple Ridge", "maple-ridge", "#2E8B57",
            "Maple Ridge chooses - at the foot of the Golden Ears.",
            "A growing city on the north bank of the Fraser River at the foot of the Golden Ears mountains.",
            [
                ("Mayor", 1, [
                    new("Sofia Rinaldi", "Environmental scientist promoting green building standards and watershed protection."),
                ]),
                ("Councillor", 6, [
                    new("Lucas Fontaine", "Urban planner focused on walkable neighbourhoods and active transportation."),
                ]),
                ("School Trustee", 3, [
                    new("Owen Gallagher", "Special education assistant advocating for student support services."),
                ]),
            ]),
    ];
}
