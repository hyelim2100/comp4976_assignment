using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Models;

namespace MunicipalElections.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options)
{
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<SavedCandidate> SavedCandidates => Set<SavedCandidate>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Municipality>()
            .HasIndex(m => m.Name)
            .IsUnique();

        builder.Entity<Position>()
            .HasOne(p => p.Municipality)
            .WithMany(m => m.Positions)
            .HasForeignKey(p => p.MunicipalityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Position>()
            .HasIndex(p => new { p.MunicipalityId, p.Name })
            .IsUnique();

        builder.Entity<Candidate>()
            .HasOne(c => c.Position)
            .WithMany(p => p.Candidates)
            .HasForeignKey(c => c.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Candidate>()
            .HasIndex(c => c.Name);

        builder.Entity<SavedCandidate>()
            .HasOne(s => s.Candidate)
            .WithMany()
            .HasForeignKey(s => s.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SavedCandidate>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SavedCandidate>()
            .HasIndex(s => new { s.UserId, s.CandidateId })
            .IsUnique();

        // Deleting a municipality also removes its admin group.
        builder.Entity<ApplicationRole>()
            .HasOne(r => r.Municipality)
            .WithMany()
            .HasForeignKey(r => r.MunicipalityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
