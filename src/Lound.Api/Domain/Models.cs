using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Lound.Api.Domain;

public enum ReportType { Lost, Found }
public enum ModerationStatus { Pending, Verified, Rejected, Removed }
public enum ItemStatus { Lost, Found, Claimed, Returned }
public enum ClaimStatus { Pending, Approved, Rejected, Returned }

public sealed class ApplicationUser : IdentityUser
{
    [MaxLength(100)] public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDirectoryRemoved { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ItemReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(100)] public string Category { get; set; } = string.Empty;
    [MaxLength(2000)] public string Description { get; set; } = string.Empty;
    [MaxLength(200)] public string Location { get; set; } = string.Empty;
    public DateOnly IncidentDate { get; set; }
    public ReportType ReportType { get; set; }
    public ModerationStatus ModerationStatus { get; set; } = ModerationStatus.Pending;
    public ItemStatus ItemStatus { get; set; }
    [MaxLength(500)] public string? ModerationReason { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Required] public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser? Owner { get; set; }
    public ICollection<OwnershipClaim> Claims { get; set; } = new List<OwnershipClaim>();
}

public sealed class OwnershipClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemReportId { get; set; }
    public ItemReport? ItemReport { get; set; }
    [Required] public string ClaimantId { get; set; } = string.Empty;
    public ApplicationUser? Claimant { get; set; }
    [MaxLength(4000)] public string Evidence { get; set; } = string.Empty;
    public ClaimStatus Status { get; set; } = ClaimStatus.Pending;
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAt { get; set; }
    public DateTimeOffset? ReturnedAt { get; set; }
    [MaxLength(500)] public string? DecisionReason { get; set; }
}

public static class RoleNames
{
    public const string User = "User";
    public const string Admin = "Admin";
}
