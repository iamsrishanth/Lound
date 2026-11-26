using System.Text.Json.Serialization;
using Lound.Api.Domain;

namespace Lound.Api.Contracts;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record UpdateProfileRequest(string DisplayName);
public sealed record UserDto(string Id, string Email, string DisplayName, IReadOnlyList<string> Roles,
    bool IsActive, bool IsDirectoryVisible, DateTimeOffset CreatedAt);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);
public sealed record CreateUserRequest(string Email, string Password, string DisplayName);

public sealed record CreateItemRequest(string Title, string Category, string Description,
    string Location, DateOnly IncidentDate, ReportType ReportType);
public sealed record UpdateItemRequest(string Title, string Category, string Description,
    string Location, DateOnly IncidentDate);
public sealed record ReasonRequest(string Reason);
public sealed record ItemDto(Guid Id, string Title, string Category, string Description, string Location,
    DateOnly IncidentDate, ReportType ReportType, ModerationStatus ModerationStatus, ItemStatus ItemStatus,
    DateTimeOffset SubmittedAt, string? OwnerDisplayName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ModerationReason = null);
public sealed record PagedItemsDto(IReadOnlyList<ItemDto> Items, int Total, int Page, int PageSize);

public sealed record CreateClaimRequest(string Evidence);
public sealed record ClaimDto(Guid Id, Guid ItemId, string ItemTitle, ClaimStatus Status,
    string Evidence, DateTimeOffset SubmittedAt, DateTimeOffset? DecidedAt,
    DateTimeOffset? ReturnedAt, string ClaimantDisplayName);
public sealed record DashboardDto(int UsersTotal, int ActiveUsers, int ReportsTotal,
    int PendingReports, int VerifiedReports, int RejectedReports, int RemovedReports,
    int LostItems, int FoundItems, int ClaimedItems, int ReturnedItems,
    int ClaimsTotal, int PendingClaims, int ApprovedClaims, int RejectedClaims, int ReturnedClaims);
public sealed record ItemReportSummary(int Total, IReadOnlyDictionary<string, int> ByModerationStatus,
    IReadOnlyDictionary<string, int> ByItemStatus);
public sealed record ClaimSummary(int Total, IReadOnlyDictionary<string, int> ByStatus);
public sealed record ReturnSummary(int Total);
public sealed record AdminSummaryDto(DateOnly From, DateOnly To, int UserRegistrations,
    ItemReportSummary Items, ClaimSummary Claims, ReturnSummary Returns);

public static class ContractValidation
{
    public static Dictionary<string, string[]> For(params (string Field, string? Value, int MaxLength, bool Required)[] fields)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (field, value, maxLength, required) in fields)
        {
            var normalized = value?.Trim();
            if (required && string.IsNullOrWhiteSpace(normalized))
                Add(field, $"{field} is required.");
            else if (normalized?.Length > maxLength)
                Add(field, $"{field} must be {maxLength} characters or fewer.");
        }
        return errors.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase);

        void Add(string key, string value)
        {
            if (!errors.TryGetValue(key, out var messages)) errors[key] = messages = new List<string>();
            messages.Add(value);
        }
    }
}
