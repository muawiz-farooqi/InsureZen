using InsureZen.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsureZen.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Claim> Claims => Set<Claim>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var claim = modelBuilder.Entity<Claim>();

        // Map PostgreSQL xmin as the concurrency token
        claim.Property(c => c.RowVersion)
             .HasColumnName("xmin")
             .HasColumnType("xid")
             .ValueGeneratedOnAddOrUpdate()
             .IsConcurrencyToken();

        // Store StandardizedData as JSONB
        claim.OwnsOne(c => c.StandardizedData, nav =>
        {
            nav.ToJson();
        });

        // Indexes for paginated queries
        claim.HasIndex(c => c.Status);
        claim.HasIndex(c => c.InsuranceCompany);
        claim.HasIndex(c => c.SubmissionDate);

        // Convert enums to strings in DB
        claim.Property(c => c.Status).HasConversion<string>();
        claim.Property(c => c.MakerRecommendation).HasConversion<string>();
        claim.Property(c => c.CheckerDecision).HasConversion<string>();
    }
}