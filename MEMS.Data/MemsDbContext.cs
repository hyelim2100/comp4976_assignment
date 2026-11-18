using MEMS.Data.Entities;
using MEMS.Data.Tenancy;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data;

// EF Core Code First context for SQL Server.
//
// Tenant isolation at the database-query level: every tenant-owned entity has a global query filter
// that reads the scope's ITenantContext. A Municipality Admin's queries therefore only ever see rows of
// their own municipality, even if a service forgets a Where clause. The filters fail closed: with no
// tenant resolved, they match nothing.
//
// Note: there is intentionally no table for voting-guide selections. Ballot choices made on the public
// site live only in the browser's sessionStorage (Election Privacy requirement).
public class MemsDbContext(DbContextOptions<MemsDbContext> options, ITenantContext tenant)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<StoredImage> Images => Set<StoredImage>();

    public ITenantContext Tenant => tenant;

    // Evaluated per query (EF parameterizes members of the context instance used in filters).
    // Only an unscoped Super Admin sees every tenant; a Super Admin scoped to one tenant does not.
    private bool BypassTenantFilter => tenant.IsSuperAdmin && tenant.MunicipalityId == null;
    private int? CurrentTenantId => tenant.MunicipalityId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Municipality>(m =>
        {
            m.HasIndex(x => x.Slug).IsUnique();
            m.HasIndex(x => x.Name).IsUnique();
            m.Property(x => x.Slug).IsUnicode(false);
            m.Property(x => x.PrimaryColor).IsUnicode(false).IsFixedLength();
            // Binary collation: the default SQL Server collation is case-insensitive ('Upper' = 'upper').
            m.ToTable(t => t.HasCheckConstraint("CK_Municipality_Slug_Lowercase",
                "[Slug] = LOWER([Slug]) COLLATE Latin1_General_BIN2 AND [Slug] NOT LIKE '% %'"));

            m.HasOne(x => x.LogoImage).WithMany().HasForeignKey(x => x.LogoImageId).OnDelete(DeleteBehavior.SetNull);

            m.HasQueryFilter(x => BypassTenantFilter || x.Id == CurrentTenantId);
        });

        builder.Entity<Position>(p =>
        {
            // Alternate key used by the candidate's composite foreign key.
            p.HasAlternateKey(x => new { x.Id, x.MunicipalityId });
            p.HasIndex(x => new { x.MunicipalityId, x.Name }).IsUnique();
            p.ToTable(t => t.HasCheckConstraint("CK_Position_Seats", "[SeatsAvailable] BETWEEN 1 AND 100"));

            p.HasOne(x => x.Municipality).WithMany(m => m.Positions)
                .HasForeignKey(x => x.MunicipalityId)
                .OnDelete(DeleteBehavior.Cascade);

            p.HasQueryFilter(x => BypassTenantFilter || x.MunicipalityId == CurrentTenantId);
        });

        builder.Entity<Candidate>(c =>
        {
            c.HasIndex(x => new { x.MunicipalityId, x.Name });

            // (PositionId, MunicipalityId) -> Position(Id, MunicipalityId): a candidate can never point to a
            // position of another tenant.
            c.HasOne(x => x.Position).WithMany(p => p.Candidates)
                .HasForeignKey(x => new { x.PositionId, x.MunicipalityId })
                .HasPrincipalKey(p => new { p.Id, p.MunicipalityId })
                .OnDelete(DeleteBehavior.Cascade);

            // NoAction: SQL Server forbids a second cascade path (Municipality -> Position -> Candidate).
            c.HasOne(x => x.Municipality).WithMany(m => m.Candidates)
                .HasForeignKey(x => x.MunicipalityId)
                .OnDelete(DeleteBehavior.NoAction);

            c.HasOne(x => x.PhotoImage).WithMany().HasForeignKey(x => x.PhotoImageId).OnDelete(DeleteBehavior.SetNull);

            c.HasQueryFilter(x => BypassTenantFilter || x.MunicipalityId == CurrentTenantId);
        });

        builder.Entity<StoredImage>(i =>
        {
            i.ToTable("Images");
            i.HasIndex(x => x.MunicipalityId);
            i.Property(x => x.ContentType).IsUnicode(false);

            i.HasQueryFilter(x => BypassTenantFilter || x.MunicipalityId == CurrentTenantId);
        });

        builder.Entity<ApplicationUser>(u =>
        {
            // Restrict: a tenant's admin accounts are removed explicitly by MunicipalityService.DeleteAsync.
            u.HasOne(x => x.Municipality).WithMany(m => m.Users)
                .HasForeignKey(x => x.MunicipalityId)
                .OnDelete(DeleteBehavior.Restrict);

            // No query filter on users: Identity must find any account at login. AdminUserService
            // (Super Admin only) is the single place that lists or edits accounts.
        });
    }
}
