# Lound API contract

This contract is the shared boundary for the ASP.NET Core API, the Windows Forms client, and integration tests. The development base URL is `http://localhost:5080`; routes start with `/v1`. JSON uses camelCase, dates are ISO 8601, and incident dates use `yyyy-MM-dd`.

## Common rules

- All routes require a valid bearer token except `POST /v1/auth/register` and `POST /v1/auth/login`.
- Tokens expire after 30 minutes. The desktop client keeps the access token in memory and clears it on sign-out.
- Roles are `User` and `Admin`. Registration always creates a `User`; only the first-admin setup command may bootstrap an administrator.
- Authenticated requests must check the current database account state. Deactivated and directory-removed accounts receive `401` even if their token has not expired.
- Standard users can search verified items, see their own reports and claims, and edit their own profile. Admin routes require the `Admin` role.
- Private claim evidence is returned only to its submitting claimant and administrators. It is never included in item search or item detail.
- Enum values are serialized as strings. IDs are opaque strings.

## Shared response shapes

Collection responses use `{ items, total, page, pageSize }`. `total` is the unpaged match count; `page` is one-based. A dashboard count is an integer, including when its value is zero.

```json
{ "code": "validation_failed", "message": "The request is invalid.", "errors": { "title": ["Title is required."] } }
```

```json
{
  "id": "opaque-id",
  "title": "Black backpack",
  "category": "Bags",
  "description": "A short item description.",
  "location": "North library",
  "incidentDate": "2026-11-26",
  "reportType": "Lost",
  "moderationStatus": "Verified",
  "itemStatus": "Lost",
  "submittedAt": "2026-11-26T10:00:00Z",
  "ownerDisplayName": "Visible only to the owner and admins"
}
```

The `ownerDisplayName` field is omitted from shared search results. Details may include it only for the report owner or an administrator. Item responses never contain claim evidence.

## Authentication and profile

| Method and route | Access | Request / response |
| --- | --- | --- |
| `POST /v1/auth/register` | Anonymous | `{ email, password, displayName }` -> `201 { user }` |
| `POST /v1/auth/login` | Anonymous | `{ email, password }` -> `{ accessToken, expiresAt, user }` |
| `POST /v1/auth/logout` | Authenticated | `204`; client clears its in-memory token |
| `GET /v1/auth/me` | Authenticated | -> `{ id, email, displayName, roles }` |
| `PATCH /v1/auth/me` | Authenticated | `{ displayName }` -> updated user |

## Item reports

New reports start with `moderationStatus: Pending`. `itemStatus` starts as `Lost` or `Found` based on `reportType`.

| Method and route | Access | Behavior |
| --- | --- | --- |
| `POST /v1/items` | User | `{ title, category, description, location, incidentDate, reportType }` -> `201` item |
| `GET /v1/items` | User | Searches verified reports only. Optional `keyword`, `category`, `location`, `from`, `to`, `reportType`, `itemStatus`, `page`, `pageSize`; returns `{ items, total, page, pageSize }`. |
| `GET /v1/items/{id}` | User | Verified item, or the caller's own report, or any item for an admin. Other pending/rejected/removed items return `404`. |
| `GET /v1/me/items` | User | All reports submitted by the caller, including moderation and item statuses. |
| `GET /v1/admin/items` | Admin | `{ items, total, page, pageSize }` for all reports, with optional moderation, item status, and date filters. |
| `PATCH /v1/admin/items/{id}` | Admin | Edits title, category, description, location, and incident date. |
| `POST /v1/admin/items/{id}/verify` | Admin | `Pending -> Verified`. |
| `POST /v1/admin/items/{id}/reject` | Admin | `Pending -> Rejected`, with `{ reason }`. |
| `POST /v1/admin/items/{id}/remove` | Admin | `Verified` or `Rejected -> Removed`, with `{ reason }`; preserves history and hides the item from shared search. |
| `POST /v1/admin/items/{id}/mark-found` | Admin | A verified `Lost` item becomes `Found` after recovery is confirmed. |

Invalid state transitions return `409 conflict`. Search filters are combined with AND semantics, text matching is case-insensitive, and results sort by incident date descending then ID ascending. Page size is capped at 100.

## Claims

A claim can be submitted only for a verified item whose current status is `Found`. Evidence is private text provided by the claimant.

```json
{ "evidence": "Private description of identifying marks or proof." }
```

Claim response shape:

```json
{
  "id": "opaque-id",
  "itemId": "opaque-id",
  "itemTitle": "Black backpack",
  "status": "Pending",
  "evidence": "Only returned to this claimant or an admin.",
  "claimantDisplayName": "Visible only to this claimant and admins.",
  "submittedAt": "2026-11-26T10:00:00Z",
  "decidedAt": null,
  "returnedAt": null
}
```

| Method and route | Access | Behavior |
| --- | --- | --- |
| `POST /v1/items/{id}/claims` | User | Creates a pending claim. Duplicate pending claims by the same claimant for the same item return `409`. |
| `GET /v1/me/claims` | User | Only the caller's claims, including their private evidence and outcomes. |
| `GET /v1/admin/claims` | Admin | `{ items, total, page, pageSize }` for all claims including evidence; optional status and date filters. |
| `POST /v1/admin/claims/{id}/approve` | Admin | Atomically approves one pending claim, changes the item `Found -> Claimed`, and rejects competing pending claims. A unique database constraint and per-item transaction prevent multiple approved claims under concurrency. |
| `POST /v1/admin/claims/{id}/reject` | Admin | Rejects a pending claim only; the item remains available as `Found`. |
| `POST /v1/admin/claims/{id}/record-return` | Admin | Records handover for the approved claim, sets `returnedAt`, and changes the item `Claimed -> Returned`. |

`claimantDisplayName` is returned only in the claimant's own claim history/response and in administrator claim review. It is never returned to other users or included in item details or search.

Claim statuses are `Pending`, `Approved`, `Rejected`, and `Returned`. An approved claim remains linked to the item after handover; its status becomes `Returned`.

## Administration

| Method and route | Access | Behavior |
| --- | --- | --- |
| `GET /v1/admin/dashboard` | Admin | Live `DashboardSummary` described below. |
| `GET /v1/admin/users` | Admin | `{ items, total, page, pageSize }` of active-directory users by default; `includeInactive=true` includes deactivated and directory-removed accounts. |
| `POST /v1/admin/users` | Admin | `{ email, password, displayName }` -> creates a standard user. |
| `POST /v1/admin/users/{id}/deactivate` | Admin | Reversibly disables sign-in and invalidates authenticated access. |
| `POST /v1/admin/users/{id}/reactivate` | Admin | Re-enables a deactivated account unless it was directory-removed. |
| `POST /v1/admin/users/{id}/remove-from-directory` | Admin | Hides the account from the active directory and revokes access, preserving history. |
| `POST /v1/admin/users/{id}/restore-to-directory` | Admin | Restores a removed account to the directory; a separately deactivated account remains inactive. |
| `GET /v1/admin/reports/summary?from=YYYY-MM-DD&to=YYYY-MM-DD` | Admin | Live date-filtered `ReportSummary` described below. Both inclusive date bounds are required and `from <= to`. |

Admin user rows have this shape:

```json
{ "id": "opaque-id", "email": "person@example.test", "displayName": "Sample User", "roles": ["User"], "isActive": true, "isDirectoryVisible": true, "createdAt": "2026-11-26T10:00:00Z" }
```

`GET /v1/admin/dashboard` returns:

```json
{
  "usersTotal": 0,
  "activeUsers": 0,
  "reportsTotal": 0,
  "pendingReports": 0,
  "verifiedReports": 0,
  "rejectedReports": 0,
  "removedReports": 0,
  "lostItems": 0,
  "foundItems": 0,
  "claimedItems": 0,
  "returnedItems": 0,
  "claimsTotal": 0,
  "pendingClaims": 0,
  "approvedClaims": 0,
  "rejectedClaims": 0,
  "returnedClaims": 0
}
```

`GET /v1/admin/reports/summary` returns:

```json
{
  "from": "2026-11-01",
  "to": "2026-11-26",
  "userRegistrations": 0,
  "items": {
    "total": 0,
    "byModerationStatus": { "Pending": 0, "Verified": 0, "Rejected": 0, "Removed": 0 },
    "byItemStatus": { "Lost": 0, "Found": 0, "Claimed": 0, "Returned": 0 }
  },
  "claims": {
    "total": 0,
    "byStatus": { "Pending": 0, "Approved": 0, "Rejected": 0, "Returned": 0 }
  },
  "returns": { "total": 0 }
}
```

Date-filtered summary item/claim/return counts use the record's creation date, decision date, or return timestamp respectively. The global dashboard counts all current records.

## Workflow and authorization invariants

1. `ModerationStatus` and `ItemStatus` are separate fields. Moderation is `Pending`, `Verified`, `Rejected`, or `Removed`; item status is `Lost`, `Found`, `Claimed`, or `Returned`.
2. Public search returns only `Verified` reports. A user's own report list may include all moderation states.
3. Only an administrator can verify, reject, remove, edit another user's report, mark a recovered item found, review evidence, decide claims, or record a return.
4. The API enforces report ownership and claim visibility on every request. Hiding controls in the client is not authorization.
5. Claim approval is one SQL transaction that locks the item, confirms `Found` and a pending claim, approves the winner, rejects competing pending claims, and changes the item to `Claimed`. A filtered unique index permits at most one `Approved` claim per item.
6. Rejecting a claim alone does not change item status. Recording a return requires an approved claim and changes the item to `Returned` in the same transaction.
7. Admin statistics are queried from current database rows, not cached or demo values.
