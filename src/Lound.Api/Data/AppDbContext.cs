using Lound.Api.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Lound.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ItemReport> ItemReports => Set<ItemReport>();
    public DbSet<OwnershipClaim> OwnershipClaims => Set<OwnershipClaim>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.IsActive, x.IsDirectoryRemoved, x.CreatedAt });
        });

        builder.Entity<ItemReport>(entity =>
        {
            entity.ToTable("ItemReports");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ModerationReason).HasMaxLength(500);
            entity.Property(x => x.ReportType).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.ModerationStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.ItemStatus).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ModerationStatus, x.ItemStatus, x.IncidentDate });
            entity.HasIndex(x => new { x.OwnerId, x.SubmittedAt });
        });

        builder.Entity<OwnershipClaim>(entity =>
        {
            entity.ToTable("OwnershipClaims");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Evidence).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DecisionReason).HasMaxLength(500);
            entity.HasOne(x => x.ItemReport).WithMany(x => x.Claims).HasForeignKey(x => x.ItemReportId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Claimant).WithMany().HasForeignKey(x => x.ClaimantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ItemReportId)
                .HasDatabaseName("IX_OwnershipClaims_OneApprovedPerItem")
                .HasFilter("[Status] = 'Approved'")
                .IsUnique();
            entity.HasIndex(x => new { x.ItemReportId, x.ClaimantId })
                .HasDatabaseName("IX_OwnershipClaims_OnePendingPerClaimantItem")
                .HasFilter("[Status] = 'Pending'")
                .IsUnique();
            entity.HasIndex(x => new { x.Status, x.SubmittedAt });
        });
    }
}
