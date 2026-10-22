using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Models;

namespace MunicipalElections.Data;

// Applies pending migrations and seeds sample data, admin groups and admin users.
// Every step is idempotent, so it is safe to run on each application start.
public static class DbSeeder
{
    private const string DefaultPassword = "P@$$w0rd";
    private static readonly DateTime ElectionDate = new(2026, 10, 17);

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.MigrateAsync();

        await SeedMunicipalitiesAsync(db);

        await EnsureRoleAsync(roleManager, AppRoles.SuperAdmin, municipalityId: null);
        foreach (var municipality in await db.Municipalities.ToListAsync())
        {
            await EnsureRoleAsync(roleManager, AppRoles.MunicipalityAdmin(municipality.Name), municipality.MunicipalityId);
        }

        await EnsureUserAsync(userManager, "super", "super@example.com", "Super Administrator", AppRoles.SuperAdmin);
        await EnsureUserAsync(userManager, "pm", "pm@example.com", "Pitt Meadows Administrator", AppRoles.MunicipalityAdmin("Pitt Meadows"));
    }

    private static async Task SeedMunicipalitiesAsync(ApplicationDbContext db)
    {
        if (await db.Municipalities.AnyAsync())
        {
            return;
        }

        db.Municipalities.AddRange(
            new Municipality
            {
                Name = "Pitt Meadows",
                Province = "British Columbia",
                ElectionDate = ElectionDate,
                Description = "A small city in Metro Vancouver bordered by the Fraser and Pitt Rivers, known for its farmland, dikes and trails.",
                Positions =
                {
                    new Position
                    {
                        Name = "Mayor",
                        SeatsAvailable = 1,
                        Candidates =
                        {
                            NewCandidate("Eleanor Whitfield", "Two-term councillor focused on affordable housing and protecting agricultural land.", "https://example.com/whitfield", "eleanor.whitfield@example.com"),
                            NewCandidate("Marcus Delgado", "Local business owner campaigning on transit connections and downtown revitalization.", "https://example.com/delgado"),
                        },
                    },
                    new Position
                    {
                        Name = "Councillor",
                        SeatsAvailable = 6,
                        Candidates =
                        {
                            NewCandidate("Priya Sandhu", "Registered nurse advocating for expanded community health services.", email: "priya.sandhu@example.com"),
                            NewCandidate("Daniel Okafor", "Civil engineer with a focus on flood protection and infrastructure renewal.", "https://example.com/okafor"),
                            NewCandidate("Grace Liu", "Youth sports coach and volunteer pushing for more parks and recreation programs."),
                            NewCandidate("Tom Hargreaves", "Retired firefighter running on public safety and emergency preparedness.", email: "tom.hargreaves@example.com"),
                        },
                    },
                    new Position
                    {
                        Name = "School Trustee",
                        SeatsAvailable = 2,
                        Candidates =
                        {
                            NewCandidate("Hannah Moreau", "Former teacher committed to smaller class sizes and inclusive education.", "https://example.com/moreau"),
                            NewCandidate("Arjun Mehta", "Parent advisory council chair focused on school safety and modern facilities.", email: "arjun.mehta@example.com"),
                        },
                    },
                },
            },
            new Municipality
            {
                Name = "Maple Ridge",
                Province = "British Columbia",
                ElectionDate = ElectionDate,
                Description = "A growing city on the north bank of the Fraser River at the foot of the Golden Ears mountains.",
                Positions =
                {
                    new Position
                    {
                        Name = "Mayor",
                        SeatsAvailable = 1,
                        Candidates =
                        {
                            NewCandidate("Robert Tanaka", "Incumbent mayor seeking a second term to complete the town centre plan.", "https://example.com/tanaka", "robert.tanaka@example.com"),
                            NewCandidate("Sofia Rinaldi", "Environmental scientist promoting green building standards and watershed protection."),
                        },
                    },
                    new Position
                    {
                        Name = "Councillor",
                        SeatsAvailable = 6,
                        Candidates =
                        {
                            NewCandidate("Kevin Byrne", "Small business advocate focused on reducing red tape for local entrepreneurs.", "https://example.com/byrne"),
                            NewCandidate("Amara Nwosu", "Social worker campaigning on homelessness prevention and mental health support.", email: "amara.nwosu@example.com"),
                            NewCandidate("Lucas Fontaine", "Urban planner focused on walkable neighbourhoods and active transportation."),
                        },
                    },
                    new Position
                    {
                        Name = "School Trustee",
                        SeatsAvailable = 3,
                        Candidates =
                        {
                            NewCandidate("Mei Chen", "Software developer pushing for stronger technology and coding programs in schools.", "https://example.com/chen"),
                            NewCandidate("Owen Gallagher", "Special education assistant advocating for student support services."),
                        },
                    },
                },
            },
            new Municipality
            {
                Name = "Burnaby",
                Province = "British Columbia",
                ElectionDate = ElectionDate,
                Description = "A large city between Vancouver and the Fraser Valley, home to Metrotown, Burnaby Mountain and extensive parkland.",
                Positions =
                {
                    new Position
                    {
                        Name = "Mayor",
                        SeatsAvailable = 1,
                        Candidates =
                        {
                            NewCandidate("Julia Kowalski", "Former city planner running on housing supply and rental protection.", "https://example.com/kowalski", "julia.kowalski@example.com"),
                            NewCandidate("Hamid Rahimi", "Community organizer focused on transit, childcare and neighbourhood services.", "https://example.com/rahimi"),
                        },
                    },
                    new Position
                    {
                        Name = "Councillor",
                        SeatsAvailable = 8,
                        Candidates =
                        {
                            NewCandidate("Nina Petrova", "Accountant committed to transparent budgeting and responsible spending.", email: "nina.petrova@example.com"),
                            NewCandidate("Samuel Wright", "Arts administrator advocating for cultural spaces and local festivals."),
                            NewCandidate("Aisha Karim", "Climate policy researcher promoting renewable energy and tree canopy growth.", "https://example.com/karim"),
                        },
                    },
                    new Position
                    {
                        Name = "School Trustee",
                        SeatsAvailable = 7,
                        Candidates =
                        {
                            NewCandidate("Diego Alvarez", "High school counsellor focused on student wellbeing and career pathways.", email: "diego.alvarez@example.com"),
                            NewCandidate("Fiona MacLeod", "Parent and librarian campaigning for literacy programs and library funding."),
                        },
                    },
                },
            });

        await db.SaveChangesAsync();
    }

    private static Candidate NewCandidate(string name, string profile, string? website = null, string? email = null) =>
        new() { Name = name, Profile = profile, Website = website, Email = email };

    private static async Task EnsureRoleAsync(RoleManager<ApplicationRole> roleManager, string roleName, int? municipalityId)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new ApplicationRole(roleName) { MunicipalityId = municipalityId });
        ThrowIfFailed(result, $"create role '{roleName}'");
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, string userName, string email, string name, string roleName)
    {
        if (await userManager.FindByNameAsync(userName) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            Name = name,
            EmailConfirmed = true,
        };

        ThrowIfFailed(await userManager.CreateAsync(user, DefaultPassword), $"create user '{userName}'");
        ThrowIfFailed(await userManager.AddToRoleAsync(user, roleName), $"add user '{userName}' to role '{roleName}'");
    }

    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to {action}: {errors}");
        }
    }
}
