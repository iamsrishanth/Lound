using System.Collections.Generic;
using Newtonsoft.Json;

namespace Lound.Desktop
{
    internal sealed class ClientSession
    {
        public ApiClient Api { get; set; }
        public UserDto User { get; set; }
    }

    internal sealed class LoginResponse
    {
        [JsonProperty("accessToken")] public string AccessToken { get; set; }
        [JsonProperty("expiresAt")] public string ExpiresAt { get; set; }
        [JsonProperty("user")] public UserDto User { get; set; }
    }

    internal sealed class UserDto
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("email")] public string Email { get; set; }
        [JsonProperty("displayName")] public string DisplayName { get; set; }
        [JsonProperty("roles")] public List<string> Roles { get; set; } = new List<string>();
        [JsonProperty("isActive")] public bool IsActive { get; set; } = true;
        [JsonProperty("isDirectoryVisible")] public bool IsDirectoryVisible { get; set; } = true;
        [JsonProperty("createdAt")] public string CreatedAt { get; set; }

        [JsonIgnore]
        public bool IsAdmin => Roles != null && Roles.Exists(r => string.Equals(r, "Admin", System.StringComparison.OrdinalIgnoreCase));
    }

    internal sealed class ItemDto
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("category")] public string Category { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("location")] public string Location { get; set; }
        [JsonProperty("incidentDate")] public string IncidentDate { get; set; }
        [JsonProperty("reportType")] public string ReportType { get; set; }
        [JsonProperty("moderationStatus")] public string ModerationStatus { get; set; }
        [JsonProperty("itemStatus")] public string ItemStatus { get; set; }
        [JsonProperty("submittedAt")] public string SubmittedAt { get; set; }
        [JsonProperty("ownerDisplayName")] public string OwnerDisplayName { get; set; }
    }

    internal sealed class ClaimDto
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("itemId")] public string ItemId { get; set; }
        [JsonProperty("itemTitle")] public string ItemTitle { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("evidence")] public string Evidence { get; set; }
        [JsonProperty("submittedAt")] public string SubmittedAt { get; set; }
        [JsonProperty("decidedAt")] public string DecidedAt { get; set; }
        [JsonProperty("returnedAt")] public string ReturnedAt { get; set; }
        [JsonProperty("claimantDisplayName")] public string ClaimantDisplayName { get; set; }
    }

    internal sealed class PageDto<T>
    {
        [JsonProperty("items")] public List<T> Items { get; set; } = new List<T>();
        [JsonProperty("total")] public int Total { get; set; }
        [JsonProperty("page")] public int Page { get; set; }
        [JsonProperty("pageSize")] public int PageSize { get; set; }
    }

    internal sealed class DashboardDto
    {
        [JsonProperty("usersTotal")] public int UsersTotal { get; set; }
        [JsonProperty("activeUsers")] public int ActiveUsers { get; set; }
        [JsonProperty("reportsTotal")] public int ReportsTotal { get; set; }
        [JsonProperty("pendingReports")] public int PendingReports { get; set; }
        [JsonProperty("verifiedReports")] public int VerifiedReports { get; set; }
        [JsonProperty("rejectedReports")] public int RejectedReports { get; set; }
        [JsonProperty("removedReports")] public int RemovedReports { get; set; }
        [JsonProperty("lostItems")] public int LostItems { get; set; }
        [JsonProperty("foundItems")] public int FoundItems { get; set; }
        [JsonProperty("claimedItems")] public int ClaimedItems { get; set; }
        [JsonProperty("returnedItems")] public int ReturnedItems { get; set; }
        [JsonProperty("claimsTotal")] public int ClaimsTotal { get; set; }
        [JsonProperty("pendingClaims")] public int PendingClaims { get; set; }
        [JsonProperty("approvedClaims")] public int ApprovedClaims { get; set; }
        [JsonProperty("rejectedClaims")] public int RejectedClaims { get; set; }
        [JsonProperty("returnedClaims")] public int ReturnedClaims { get; set; }
    }

    internal sealed class ApiErrorDto
    {
        [JsonProperty("code")] public string Code { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("errors")] public Dictionary<string, string[]> Errors { get; set; }
    }
}
