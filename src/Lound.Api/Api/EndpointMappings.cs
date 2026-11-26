using System.Data;
using System.Globalization;
using Lound.Api.Contracts;
using Lound.Api.Data;
using Lound.Api.Domain;
using Lound.Api.Infrastructure;
using Lound.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lound.Api.Api;

public static class EndpointMappings
{
    private const string CaseInsensitiveCollation = "Latin1_General_100_CI_AI";

    public static void MapLoundEndpoints(this WebApplication app)
    {
        MapAuthentication(app);
        MapItems(app);
        MapClaims(app);
        MapAdmin(app);
    }

    private static void MapAuthentication(WebApplication app)
    {
        var auth = app.MapGroup("/v1/auth");
        auth.MapPost("/register", RegisterAsync).AllowAnonymous();
        auth.MapPost("/login", LoginAsync).AllowAnonymous();
        auth.MapPost("/logout", () => Results.NoContent()).RequireAuthorization();
        auth.MapGet("/me", GetMeAsync).RequireAuthorization();
        auth.MapPatch("/me", UpdateProfileAsync).RequireAuthorization();
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest request,
        UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles,
        CancellationToken cancellationToken)
    {
        var errors = ContractValidation.For(
            ("email", request.Email, 256, true), ("displayName", request.DisplayName, 100, true));
        var email = ApiResults.Clean(request.Email);
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            errors["email"] = new[] { "Enter a valid email address." };
        if (string.IsNullOrWhiteSpace(request.Password))
            errors["password"] = new[] { "Password is required." };
        if (errors.Count > 0) return ApiResults.Validation(errors);

        if (!await roles.RoleExistsAsync(RoleNames.User))
        {
            var createdRole = await roles.CreateAsync(new IdentityRole(RoleNames.User));
            if (!createdRole.Succeeded) return ApiResults.IdentityErrors(createdRole.Errors);
        }
        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            DisplayName = ApiResults.Clean(request.DisplayName)
        };
        var create = await users.CreateAsync(user, request.Password);
        if (!create.Succeeded) return ApiResults.IdentityErrors(create.Errors);
        var addRole = await users.AddToRoleAsync(user, RoleNames.User);
        if (!addRole.Succeeded)
        {
            await users.DeleteAsync(user);
            return ApiResults.IdentityErrors(addRole.Errors);
        }
        return Results.Created("/v1/auth/me", new { user = await ApiResults.ToUserDtoAsync(user, users) });
    }

    private static async Task<IResult> LoginAsync(LoginRequest request,
        UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        TokenIssuer tokens)
    {
        var email = ApiResults.Clean(request.Email);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(request.Password))
            return ApiResults.Error(StatusCodes.Status401Unauthorized, "invalid_credentials", "Email or password is incorrect.");
        var user = await users.FindByEmailAsync(email);
        if (user is null || !user.IsActive || user.IsDirectoryRemoved)
            return ApiResults.Error(StatusCodes.Status401Unauthorized, "invalid_credentials", "Email or password is incorrect.");
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return ApiResults.Error(StatusCodes.Status401Unauthorized, "invalid_credentials", "Email or password is incorrect.");
        var userRoles = await users.GetRolesAsync(user);
        return Results.Ok(tokens.Create(user, userRoles.ToArray()));
    }

    private static async Task<IResult> GetMeAsync(HttpContext context, UserManager<ApplicationUser> users)
    {
        var user = await users.FindByIdAsync(ApiResults.UserId(context.User)!);
        return user is null ? ApiResults.NotFound() : Results.Ok(await ApiResults.ToUserDtoAsync(user, users));
    }

    private static async Task<IResult> UpdateProfileAsync(UpdateProfileRequest request, HttpContext context,
        UserManager<ApplicationUser> users)
    {
        var errors = ContractValidation.For(("displayName", request.DisplayName, 100, true));
        if (errors.Count > 0) return ApiResults.Validation(errors);
        var user = await users.FindByIdAsync(ApiResults.UserId(context.User)!);
        if (user is null) return ApiResults.NotFound();
        user.DisplayName = ApiResults.Clean(request.DisplayName);
        var update = await users.UpdateAsync(user);
        if (!update.Succeeded) return ApiResults.IdentityErrors(update.Errors);
        return Results.Ok(await ApiResults.ToUserDtoAsync(user, users));
    }

    private static void MapItems(WebApplication app)
    {
        var items = app.MapGroup("/v1/items").RequireAuthorization();
        items.MapPost("", CreateItemAsync);
        items.MapGet("", SearchItemsAsync);
        items.MapGet("/{id:guid}", GetItemAsync);

        var mine = app.MapGroup("/v1/me").RequireAuthorization();
        mine.MapGet("/items", GetMyItemsAsync);
    }

    private static async Task<IResult> CreateItemAsync(CreateItemRequest request, HttpContext context, AppDbContext db)
    {
        var errors = ContractValidation.For(
            ("title", request.Title, 200, true), ("category", request.Category, 100, true),
            ("description", request.Description, 2000, true), ("location", request.Location, 200, true));
        if (!Enum.IsDefined(request.ReportType)) errors["reportType"] = new[] { "Report type must be Lost or Found." };
        if (request.IncidentDate > DateOnly.FromDateTime(DateTime.UtcNow))
            errors["incidentDate"] = new[] { "Incident date cannot be in the future." };
        if (errors.Count > 0) return ApiResults.Validation(errors);

        var reportType = request.ReportType;
        var now = DateTimeOffset.UtcNow;
        var item = new ItemReport
        {
            Title = ApiResults.Clean(request.Title),
            Category = ApiResults.Clean(request.Category),
            Description = ApiResults.Clean(request.Description),
            Location = ApiResults.Clean(request.Location),
            IncidentDate = request.IncidentDate,
            ReportType = reportType,
            ModerationStatus = ModerationStatus.Pending,
            ItemStatus = reportType == ReportType.Lost ? ItemStatus.Lost : ItemStatus.Found,
            OwnerId = ApiResults.UserId(context.User)!,
            SubmittedAt = now,
            UpdatedAt = now
        };
        db.ItemReports.Add(item);
        await db.SaveChangesAsync();
        return Results.Created($"/v1/items/{item.Id}", ToItemDto(item));
    }

    private static async Task<IResult> SearchItemsAsync(AppDbContext db, string? keyword = null,
        string? category = null, string? location = null, DateOnly? from = null, DateOnly? to = null,
        string? reportType = null, string? itemStatus = null, int page = 1, int pageSize = 25)
    {
        if (from.HasValue && to.HasValue && from > to)
            return ApiResults.Validation(new Dictionary<string, string[]> { ["dateRange"] = new[] { "From date must not be after To date." } });
        var query = db.ItemReports.AsNoTracking().Where(x => x.ModerationStatus == ModerationStatus.Verified);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x =>
                EF.Functions.Collate(x.Title, CaseInsensitiveCollation).Contains(term) ||
                EF.Functions.Collate(x.Description, CaseInsensitiveCollation).Contains(term) ||
                EF.Functions.Collate(x.Category, CaseInsensitiveCollation).Contains(term) ||
                EF.Functions.Collate(x.Location, CaseInsensitiveCollation).Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => EF.Functions.Collate(x.Category, CaseInsensitiveCollation).Contains(category.Trim()));
        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(x => EF.Functions.Collate(x.Location, CaseInsensitiveCollation).Contains(location.Trim()));
        if (from.HasValue) query = query.Where(x => x.IncidentDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.IncidentDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(reportType))
        {
            if (!ApiResults.TryParseEnum<ReportType>(reportType, out var parsed))
                return ApiResults.Validation(new Dictionary<string, string[]> { ["reportType"] = new[] { "Report type is invalid." } });
            query = query.Where(x => x.ReportType == parsed);
        }
        if (!string.IsNullOrWhiteSpace(itemStatus))
        {
            if (!ApiResults.TryParseEnum<ItemStatus>(itemStatus, out var parsed))
                return ApiResults.Validation(new Dictionary<string, string[]> { ["itemStatus"] = new[] { "Item status is invalid." } });
            query = query.Where(x => x.ItemStatus == parsed);
        }

        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var total = await query.CountAsync();
        var data = await query.OrderByDescending(x => x.IncidentDate).ThenBy(x => x.Id)
            .Skip(Skip(normalizedPage, normalizedSize)).Take(normalizedSize)
            .Select(x => new ItemDto(x.Id, x.Title, x.Category, x.Description, x.Location, x.IncidentDate,
                x.ReportType, x.ModerationStatus, x.ItemStatus, x.SubmittedAt))
            .ToListAsync();
        return Results.Ok(new PagedResponse<ItemDto>(data, total, normalizedPage, normalizedSize));
    }

    private static async Task<IResult> GetItemAsync(Guid id, HttpContext context, AppDbContext db)
    {
        var isAdmin = context.User.IsInRole(RoleNames.Admin);
        var userId = ApiResults.UserId(context.User)!;
        var item = await db.ItemReports.AsNoTracking().Include(x => x.Owner).SingleOrDefaultAsync(x => x.Id == id);
        if (item is null || (item.ModerationStatus != ModerationStatus.Verified && !isAdmin && item.OwnerId != userId))
            return ApiResults.NotFound();
        return Results.Ok(ToItemDto(item, isAdmin || item.OwnerId == userId ? item.Owner?.DisplayName : null,
            isAdmin ? item.ModerationReason : null));
    }

    private static async Task<IResult> GetMyItemsAsync(HttpContext context, AppDbContext db,
        int page = 1, int pageSize = 25)
    {
        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var query = db.ItemReports.AsNoTracking().Where(x => x.OwnerId == ApiResults.UserId(context.User));
        var total = await query.CountAsync();
        var data = await query.OrderByDescending(x => x.SubmittedAt).ThenBy(x => x.Id)
            .Skip(Skip(normalizedPage, normalizedSize)).Take(normalizedSize)
            .Select(x => new ItemDto(x.Id, x.Title, x.Category, x.Description, x.Location, x.IncidentDate,
                x.ReportType, x.ModerationStatus, x.ItemStatus, x.SubmittedAt, null, x.ModerationReason))
            .ToListAsync();
        return Results.Ok(new PagedResponse<ItemDto>(data, total, normalizedPage, normalizedSize));
    }

    private static ItemDto ToItemDto(ItemReport item, string? ownerDisplayName = null, string? moderationReason = null) =>
        new(item.Id, item.Title, item.Category, item.Description, item.Location, item.IncidentDate,
            item.ReportType, item.ModerationStatus, item.ItemStatus, item.SubmittedAt,
            ownerDisplayName, moderationReason);

    private static void MapClaims(WebApplication app)
    {
        var claims = app.MapGroup("/v1/me").RequireAuthorization();
        claims.MapGet("/claims", GetMyClaimsAsync);

        var itemClaims = app.MapGroup("/v1/items").RequireAuthorization();
        itemClaims.MapPost("/{id:guid}/claims", SubmitClaimAsync);
    }

    private static async Task<IResult> SubmitClaimAsync(Guid id, CreateClaimRequest request,
        HttpContext context, AppDbContext db, UserManager<ApplicationUser> users)
    {
        var errors = ContractValidation.For(("evidence", request.Evidence, 4000, true));
        if (errors.Count > 0) return ApiResults.Validation(errors);
        var userId = ApiResults.UserId(context.User)!;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var item = await LockItemAsync(db, id);
        if (item is null || item.ModerationStatus != ModerationStatus.Verified)
            return ApiResults.NotFound("A verified item was not found.");
        if (item.ItemStatus != ItemStatus.Found)
            return ApiResults.Conflict("Claims can be submitted only for items currently marked Found.");
        if (await db.OwnershipClaims.AnyAsync(x => x.ItemReportId == id && x.ClaimantId == userId && x.Status == ClaimStatus.Pending))
            return ApiResults.Conflict("You already have a pending claim for this item.");

        var claim = new OwnershipClaim
        {
            ItemReportId = id,
            ClaimantId = userId,
            Evidence = ApiResults.Clean(request.Evidence),
            Status = ClaimStatus.Pending,
            SubmittedAt = DateTimeOffset.UtcNow
        };
        db.OwnershipClaims.Add(claim);
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException exception) when (ApiResults.IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync();
            return ApiResults.Conflict("A pending claim already exists for this claimant and item.");
        }
        var claimant = await users.FindByIdAsync(userId);
        return Results.Created($"/v1/me/claims/{claim.Id}",
            ToClaimDto(claim, item.Title, claimant?.DisplayName ?? string.Empty));
    }

    private static async Task<IResult> GetMyClaimsAsync(HttpContext context, AppDbContext db,
        int page = 1, int pageSize = 25)
    {
        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var query = db.OwnershipClaims.AsNoTracking()
            .Where(x => x.ClaimantId == ApiResults.UserId(context.User));
        var total = await query.CountAsync();
        var data = await query.Include(x => x.ItemReport).Include(x => x.Claimant)
            .OrderByDescending(x => x.SubmittedAt).ThenBy(x => x.Id)
            .Skip(Skip(normalizedPage, normalizedSize)).Take(normalizedSize)
            .Select(x => new ClaimDto(x.Id, x.ItemReportId, x.ItemReport!.Title, x.Status,
                x.Evidence, x.SubmittedAt, x.DecidedAt, x.ReturnedAt,
                x.Claimant!.DisplayName))
            .ToListAsync();
        return Results.Ok(new PagedResponse<ClaimDto>(data, total, normalizedPage, normalizedSize));
    }

    private static ClaimDto ToClaimDto(OwnershipClaim claim, string itemTitle, string claimantName) =>
        new(claim.Id, claim.ItemReportId, itemTitle, claim.Status, claim.Evidence,
            claim.SubmittedAt, claim.DecidedAt, claim.ReturnedAt, claimantName);

    private static async Task<ItemReport?> LockItemAsync(AppDbContext db, Guid id) =>
        await db.ItemReports.FromSqlInterpolated(
            $"SELECT * FROM [dbo].[ItemReports] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
            .SingleOrDefaultAsync();

    private static void MapAdmin(WebApplication app)
    {
        var admin = app.MapGroup("/v1/admin").RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        admin.MapGet("/items", GetAdminItemsAsync);
        admin.MapPatch("/items/{id:guid}", UpdateAdminItemAsync);
        admin.MapPost("/items/{id:guid}/verify", VerifyItemAsync);
        admin.MapPost("/items/{id:guid}/reject", RejectItemAsync);
        admin.MapPost("/items/{id:guid}/remove", RemoveItemAsync);
        admin.MapPost("/items/{id:guid}/mark-found", MarkFoundAsync);
        admin.MapGet("/claims", GetAdminClaimsAsync);
        admin.MapPost("/claims/{id:guid}/approve", ApproveClaimAsync);
        admin.MapPost("/claims/{id:guid}/reject", RejectClaimAsync);
        admin.MapPost("/claims/{id:guid}/record-return", RecordReturnAsync);
        admin.MapGet("/users", GetAdminUsersAsync);
        admin.MapPost("/users", CreateAdminUserAsync);
        admin.MapPost("/users/{id}/deactivate", DeactivateUserAsync);
        admin.MapPost("/users/{id}/reactivate", ReactivateUserAsync);
        admin.MapPost("/users/{id}/remove-from-directory", RemoveUserFromDirectoryAsync);
        admin.MapPost("/users/{id}/restore-to-directory", RestoreUserToDirectoryAsync);
        admin.MapGet("/dashboard", GetDashboardAsync);
        admin.MapGet("/reports/summary", GetSummaryAsync);
    }

    private static async Task<IResult> GetAdminItemsAsync(AppDbContext db, string? moderationStatus = null,
        string? itemStatus = null, DateOnly? from = null, DateOnly? to = null,
        int page = 1, int pageSize = 25)
    {
        if (from.HasValue && to.HasValue && from > to)
            return ApiResults.Validation(new Dictionary<string, string[]> { ["dateRange"] = new[] { "From date must not be after To date." } });
        var query = db.ItemReports.AsNoTracking().Include(x => x.Owner).AsQueryable();
        if (!string.IsNullOrWhiteSpace(moderationStatus))
        {
            if (!ApiResults.TryParseEnum<ModerationStatus>(moderationStatus, out var parsed))
                return ApiResults.Validation(new Dictionary<string, string[]> { ["moderationStatus"] = new[] { "Moderation status is invalid." } });
            query = query.Where(x => x.ModerationStatus == parsed);
        }
        if (!string.IsNullOrWhiteSpace(itemStatus))
        {
            if (!ApiResults.TryParseEnum<ItemStatus>(itemStatus, out var parsed))
                return ApiResults.Validation(new Dictionary<string, string[]> { ["itemStatus"] = new[] { "Item status is invalid." } });
            query = query.Where(x => x.ItemStatus == parsed);
        }
        if (from.HasValue) query = query.Where(x => x.IncidentDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.IncidentDate <= to.Value);
        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var total = await query.CountAsync();
        var data = await query.OrderByDescending(x => x.SubmittedAt).ThenBy(x => x.Id)
            .Skip(Skip(normalizedPage, normalizedSize)).Take(normalizedSize)
            .Select(x => new ItemDto(x.Id, x.Title, x.Category, x.Description, x.Location, x.IncidentDate,
                x.ReportType, x.ModerationStatus, x.ItemStatus, x.SubmittedAt, x.Owner!.DisplayName,
                x.ModerationReason))
            .ToListAsync();
        return Results.Ok(new PagedResponse<ItemDto>(data, total, normalizedPage, normalizedSize));
    }

    private static async Task<IResult> UpdateAdminItemAsync(Guid id, UpdateItemRequest request, AppDbContext db)
    {
        var errors = ContractValidation.For(
            ("title", request.Title, 200, true), ("category", request.Category, 100, true),
            ("description", request.Description, 2000, true), ("location", request.Location, 200, true));
        if (request.IncidentDate > DateOnly.FromDateTime(DateTime.UtcNow))
            errors["incidentDate"] = new[] { "Incident date cannot be in the future." };
        if (errors.Count > 0) return ApiResults.Validation(errors);
        var item = await db.ItemReports.Include(x => x.Owner).SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return ApiResults.NotFound();
        item.Title = ApiResults.Clean(request.Title);
        item.Category = ApiResults.Clean(request.Category);
        item.Description = ApiResults.Clean(request.Description);
        item.Location = ApiResults.Clean(request.Location);
        item.IncidentDate = request.IncidentDate;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(ToItemDto(item, item.Owner?.DisplayName, item.ModerationReason));
    }

    private static async Task<IResult> VerifyItemAsync(Guid id, AppDbContext db)
    {
        var item = await db.ItemReports.Include(x => x.Owner).SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return ApiResults.NotFound();
        if (item.ModerationStatus != ModerationStatus.Pending)
            return ApiResults.Conflict("Only pending reports can be verified.");
        item.ModerationStatus = ModerationStatus.Verified;
        item.ModerationReason = null;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(ToItemDto(item, item.Owner?.DisplayName));
    }

    private static async Task<IResult> RejectItemAsync(Guid id, ReasonRequest request, AppDbContext db) =>
        await SetModerationAsync(id, request, ModerationStatus.Rejected, db);

    private static async Task<IResult> RemoveItemAsync(Guid id, ReasonRequest request, AppDbContext db) =>
        await SetModerationAsync(id, request, ModerationStatus.Removed, db);

    private static async Task<IResult> SetModerationAsync(Guid id, ReasonRequest request,
        ModerationStatus target, AppDbContext db)
    {
        var errors = ContractValidation.For(("reason", request.Reason, 500, true));
        if (errors.Count > 0) return ApiResults.Validation(errors);
        var item = await db.ItemReports.Include(x => x.Owner).SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return ApiResults.NotFound();
        var allowed = target == ModerationStatus.Rejected
            ? item.ModerationStatus == ModerationStatus.Pending
            : item.ModerationStatus is ModerationStatus.Verified or ModerationStatus.Rejected;
        if (!allowed) return ApiResults.Conflict(target == ModerationStatus.Rejected
            ? "Only pending reports can be rejected."
            : "Only verified or rejected reports can be removed.");
        item.ModerationStatus = target;
        item.ModerationReason = ApiResults.Clean(request.Reason);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(ToItemDto(item, item.Owner?.DisplayName, item.ModerationReason));
    }

    private static async Task<IResult> MarkFoundAsync(Guid id, AppDbContext db)
    {
        var item = await db.ItemReports.Include(x => x.Owner).SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return ApiResults.NotFound();
        if (item.ModerationStatus != ModerationStatus.Verified || item.ItemStatus != ItemStatus.Lost)
            return ApiResults.Conflict("Only a verified Lost item can be marked Found.");
        item.ItemStatus = ItemStatus.Found;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(ToItemDto(item, item.Owner?.DisplayName));
    }

    private static async Task<IResult> GetAdminClaimsAsync(AppDbContext db, string? status = null,
        DateOnly? from = null, DateOnly? to = null, int page = 1, int pageSize = 25)
    {
        if (from.HasValue && to.HasValue && from > to)
            return ApiResults.Validation(new Dictionary<string, string[]> { ["dateRange"] = new[] { "From date must not be after To date." } });
        var query = db.OwnershipClaims.AsNoTracking().Include(x => x.ItemReport).Include(x => x.Claimant).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!ApiResults.TryParseEnum<ClaimStatus>(status, out var parsed))
                return ApiResults.Validation(new Dictionary<string, string[]> { ["status"] = new[] { "Claim status is invalid." } });
            query = query.Where(x => x.Status == parsed);
        }
        if (from.HasValue) query = query.Where(x => x.SubmittedAt >= UtcStart(from.Value));
        if (to.HasValue) query = query.Where(x => x.SubmittedAt < UtcEndExclusive(to.Value));
        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var total = await query.CountAsync();
        var data = await query.OrderByDescending(x => x.SubmittedAt).ThenBy(x => x.Id)
            .Skip(Skip(normalizedPage, normalizedSize)).Take(normalizedSize)
            .Select(x => new ClaimDto(x.Id, x.ItemReportId, x.ItemReport!.Title, x.Status,
                x.Evidence, x.SubmittedAt, x.DecidedAt, x.ReturnedAt, x.Claimant!.DisplayName))
            .ToListAsync();
        return Results.Ok(new PagedResponse<ClaimDto>(data, total, normalizedPage, normalizedSize));
    }

    private static async Task<IResult> ApproveClaimAsync(Guid id, AppDbContext db)
    {
        var claimSnapshot = await db.OwnershipClaims.AsNoTracking()
            .Where(x => x.Id == id).Select(x => new { x.ItemReportId }).SingleOrDefaultAsync();
        if (claimSnapshot is null) return ApiResults.NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var item = await LockItemAsync(db, claimSnapshot.ItemReportId);
        if (item is null) return ApiResults.NotFound();
        var claim = await db.OwnershipClaims.Include(x => x.Claimant).SingleOrDefaultAsync(x => x.Id == id);
        if (claim is null) return ApiResults.NotFound();
        if (claim.Status != ClaimStatus.Pending)
            return ApiResults.Conflict("Only pending claims can be approved.");
        if (item.ModerationStatus != ModerationStatus.Verified || item.ItemStatus != ItemStatus.Found)
            return ApiResults.Conflict("The item must be verified and currently marked Found.");

        var now = DateTimeOffset.UtcNow;
        claim.Status = ClaimStatus.Approved;
        claim.DecidedAt = now;
        claim.DecisionReason = null;
        item.ItemStatus = ItemStatus.Claimed;
        item.UpdatedAt = now;
        var competingClaims = await db.OwnershipClaims
            .Where(x => x.ItemReportId == item.Id && x.Id != claim.Id && x.Status == ClaimStatus.Pending)
            .ToListAsync();
        foreach (var competitor in competingClaims)
        {
            competitor.Status = ClaimStatus.Rejected;
            competitor.DecidedAt = now;
            competitor.DecisionReason = "Another claim for this item was approved.";
        }
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException exception) when (ApiResults.IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync();
            return ApiResults.Conflict("Another claim for this item has already been approved.");
        }
        return Results.Ok(ToClaimDto(claim, item.Title, claim.Claimant?.DisplayName ?? string.Empty));
    }

    private static async Task<IResult> RejectClaimAsync(Guid id, ReasonRequest request, AppDbContext db)
    {
        var errors = ContractValidation.For(("reason", request.Reason, 500, true));
        if (errors.Count > 0) return ApiResults.Validation(errors);
        var snapshot = await db.OwnershipClaims.AsNoTracking()
            .Where(x => x.Id == id).Select(x => new { x.ItemReportId }).SingleOrDefaultAsync();
        if (snapshot is null) return ApiResults.NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var item = await LockItemAsync(db, snapshot.ItemReportId);
        if (item is null) return ApiResults.NotFound();
        var claim = await db.OwnershipClaims.Include(x => x.Claimant).SingleOrDefaultAsync(x => x.Id == id);
        if (claim is null) return ApiResults.NotFound();
        if (claim.Status != ClaimStatus.Pending)
            return ApiResults.Conflict("Only pending claims can be rejected.");
        claim.Status = ClaimStatus.Rejected;
        claim.DecidedAt = DateTimeOffset.UtcNow;
        claim.DecisionReason = ApiResults.Clean(request.Reason);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(ToClaimDto(claim, item.Title, claim.Claimant?.DisplayName ?? string.Empty));
    }

    private static async Task<IResult> RecordReturnAsync(Guid id, AppDbContext db)
    {
        var snapshot = await db.OwnershipClaims.AsNoTracking()
            .Where(x => x.Id == id).Select(x => new { x.ItemReportId }).SingleOrDefaultAsync();
        if (snapshot is null) return ApiResults.NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var item = await LockItemAsync(db, snapshot.ItemReportId);
        if (item is null) return ApiResults.NotFound();
        var claim = await db.OwnershipClaims.Include(x => x.Claimant).SingleOrDefaultAsync(x => x.Id == id);
        if (claim is null) return ApiResults.NotFound();
        if (claim.Status != ClaimStatus.Approved || item.ItemStatus != ItemStatus.Claimed)
            return ApiResults.Conflict("Only an approved claim for a Claimed item can be returned.");
        var now = DateTimeOffset.UtcNow;
        claim.Status = ClaimStatus.Returned;
        claim.ReturnedAt = now;
        item.ItemStatus = ItemStatus.Returned;
        item.UpdatedAt = now;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(ToClaimDto(claim, item.Title, claim.Claimant?.DisplayName ?? string.Empty));
    }

    private static async Task<IResult> GetAdminUsersAsync(AppDbContext db, UserManager<ApplicationUser> users,
        bool includeInactive = false, int page = 1, int pageSize = 25)
    {
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!includeInactive) query = query.Where(x => x.IsActive && !x.IsDirectoryRemoved);
        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var total = await query.CountAsync();
        var pageUsers = await query.OrderBy(x => x.NormalizedEmail).ThenBy(x => x.Id)
            .Skip(Skip(normalizedPage, normalizedSize)).Take(normalizedSize).ToListAsync();
        var data = new List<UserDto>(pageUsers.Count);
        foreach (var user in pageUsers) data.Add(await ApiResults.ToUserDtoAsync(user, users));
        return Results.Ok(new PagedResponse<UserDto>(data, total, normalizedPage, normalizedSize));
    }

    private static async Task<IResult> CreateAdminUserAsync(CreateUserRequest request,
        UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
    {
        var email = ApiResults.Clean(request.Email);
        var errors = ContractValidation.For(("email", email, 256, true), ("displayName", request.DisplayName, 100, true));
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            errors["email"] = new[] { "Enter a valid email address." };
        if (string.IsNullOrWhiteSpace(request.Password)) errors["password"] = new[] { "Password is required." };
        if (errors.Count > 0) return ApiResults.Validation(errors);
        if (!await roles.RoleExistsAsync(RoleNames.User))
        {
            var createRole = await roles.CreateAsync(new IdentityRole(RoleNames.User));
            if (!createRole.Succeeded) return ApiResults.IdentityErrors(createRole.Errors);
        }
        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            DisplayName = ApiResults.Clean(request.DisplayName)
        };
        var create = await users.CreateAsync(user, request.Password);
        if (!create.Succeeded) return ApiResults.IdentityErrors(create.Errors);
        var addRole = await users.AddToRoleAsync(user, RoleNames.User);
        if (!addRole.Succeeded)
        {
            await users.DeleteAsync(user);
            return ApiResults.IdentityErrors(addRole.Errors);
        }
        return Results.Created($"/v1/admin/users/{user.Id}", await ApiResults.ToUserDtoAsync(user, users));
    }

    private static async Task<IResult> DeactivateUserAsync(string id, HttpContext context,
        UserManager<ApplicationUser> users)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return ApiResults.NotFound();
        if (user.IsActive && !await HasOtherActiveAdminAsync(user, users))
            return ApiResults.Conflict("The last active administrator cannot be deactivated.");
        user.IsActive = false;
        var result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) return ApiResults.IdentityErrors(result.Errors);
        return Results.Ok(await ApiResults.ToUserDtoAsync(user, users));
    }

    private static async Task<IResult> ReactivateUserAsync(string id, UserManager<ApplicationUser> users)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return ApiResults.NotFound();
        if (user.IsDirectoryRemoved)
            return ApiResults.Conflict("Restore this account to the directory before reactivating it.");
        user.IsActive = true;
        var result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) return ApiResults.IdentityErrors(result.Errors);
        return Results.Ok(await ApiResults.ToUserDtoAsync(user, users));
    }

    private static async Task<IResult> RemoveUserFromDirectoryAsync(string id,
        UserManager<ApplicationUser> users)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return ApiResults.NotFound();
        if (!user.IsDirectoryRemoved && !await HasOtherActiveAdminAsync(user, users))
            return ApiResults.Conflict("The last active administrator cannot be removed from the directory.");
        user.IsDirectoryRemoved = true;
        var result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) return ApiResults.IdentityErrors(result.Errors);
        return Results.Ok(await ApiResults.ToUserDtoAsync(user, users));
    }

    private static async Task<IResult> RestoreUserToDirectoryAsync(string id, UserManager<ApplicationUser> users)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return ApiResults.NotFound();
        user.IsDirectoryRemoved = false;
        var result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) return ApiResults.IdentityErrors(result.Errors);
        return Results.Ok(await ApiResults.ToUserDtoAsync(user, users));
    }

    private static async Task<bool> HasOtherActiveAdminAsync(ApplicationUser target, UserManager<ApplicationUser> users)
    {
        if (!await users.IsInRoleAsync(target, RoleNames.Admin)) return true;
        var admins = await users.GetUsersInRoleAsync(RoleNames.Admin);
        return admins.Any(x => x.Id != target.Id && x.IsActive && !x.IsDirectoryRemoved);
    }

    private static async Task<IResult> GetDashboardAsync(AppDbContext db)
    {
        var usersTotal = await db.Users.CountAsync();
        var activeUsers = await db.Users.CountAsync(x => x.IsActive && !x.IsDirectoryRemoved);
        var reportsTotal = await db.ItemReports.CountAsync();
        var pendingReports = await db.ItemReports.CountAsync(x => x.ModerationStatus == ModerationStatus.Pending);
        var verifiedReports = await db.ItemReports.CountAsync(x => x.ModerationStatus == ModerationStatus.Verified);
        var rejectedReports = await db.ItemReports.CountAsync(x => x.ModerationStatus == ModerationStatus.Rejected);
        var removedReports = await db.ItemReports.CountAsync(x => x.ModerationStatus == ModerationStatus.Removed);
        var lostItems = await db.ItemReports.CountAsync(x => x.ItemStatus == ItemStatus.Lost);
        var foundItems = await db.ItemReports.CountAsync(x => x.ItemStatus == ItemStatus.Found);
        var claimedItems = await db.ItemReports.CountAsync(x => x.ItemStatus == ItemStatus.Claimed);
        var returnedItems = await db.ItemReports.CountAsync(x => x.ItemStatus == ItemStatus.Returned);
        var claimsTotal = await db.OwnershipClaims.CountAsync();
        var pendingClaims = await db.OwnershipClaims.CountAsync(x => x.Status == ClaimStatus.Pending);
        var approvedClaims = await db.OwnershipClaims.CountAsync(x => x.Status == ClaimStatus.Approved);
        var rejectedClaims = await db.OwnershipClaims.CountAsync(x => x.Status == ClaimStatus.Rejected);
        var returnedClaims = await db.OwnershipClaims.CountAsync(x => x.Status == ClaimStatus.Returned);
        return Results.Ok(new DashboardDto(usersTotal, activeUsers, reportsTotal, pendingReports,
            verifiedReports, rejectedReports, removedReports, lostItems, foundItems, claimedItems,
            returnedItems, claimsTotal, pendingClaims, approvedClaims, rejectedClaims, returnedClaims));
    }

    private static async Task<IResult> GetSummaryAsync(AppDbContext db, string? from = null, string? to = null)
    {
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate) ||
            !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
            return ApiResults.Validation(new Dictionary<string, string[]> { ["dateRange"] = new[] { "Provide from and to as yyyy-MM-dd dates." } });
        if (fromDate > toDate)
            return ApiResults.Validation(new Dictionary<string, string[]> { ["dateRange"] = new[] { "From date must not be after To date." } });

        var start = UtcStart(fromDate);
        var end = toDate == DateOnly.MaxValue ? (DateTimeOffset?)null : UtcEndExclusive(toDate);
        var usersQuery = db.Users.AsNoTracking().Where(x => x.CreatedAt >= start);
        var itemQuery = db.ItemReports.AsNoTracking().Where(x => x.SubmittedAt >= start);
        var claimQuery = db.OwnershipClaims.AsNoTracking()
            .Where(x => (x.DecidedAt ?? x.SubmittedAt) >= start);
        var returnQuery = db.OwnershipClaims.AsNoTracking().Where(x => x.ReturnedAt != null && x.ReturnedAt >= start);
        if (end.HasValue)
        {
            usersQuery = usersQuery.Where(x => x.CreatedAt < end.Value);
            itemQuery = itemQuery.Where(x => x.SubmittedAt < end.Value);
            claimQuery = claimQuery.Where(x => (x.DecidedAt ?? x.SubmittedAt) < end.Value);
            returnQuery = returnQuery.Where(x => x.ReturnedAt < end.Value);
        }
        var moderationCounts = await itemQuery.GroupBy(x => x.ModerationStatus)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Status, x => x.Count);
        var itemStatusCounts = await itemQuery.GroupBy(x => x.ItemStatus)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Status, x => x.Count);
        var claimCounts = await claimQuery.GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Status, x => x.Count);

        var itemTotal = await itemQuery.CountAsync();
        var claimTotal = await claimQuery.CountAsync();
        var returnTotal = await returnQuery.CountAsync();
        var registrations = await usersQuery.CountAsync();
        return Results.Ok(new AdminSummaryDto(fromDate, toDate, registrations,
            new ItemReportSummary(itemTotal, AllCounts<ModerationStatus>(moderationCounts),
                AllCounts<ItemStatus>(itemStatusCounts)),
            new ClaimSummary(claimTotal, AllCounts<ClaimStatus>(claimCounts)),
            new ReturnSummary(returnTotal)));
    }

    private static IReadOnlyDictionary<string, int> AllCounts<TEnum>(IReadOnlyDictionary<TEnum, int> counts)
        where TEnum : struct, Enum => Enum.GetValues<TEnum>()
        .ToDictionary(x => x.ToString(), x => counts.TryGetValue(x, out var count) ? count : 0);

    private static DateTimeOffset UtcStart(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    private static DateTimeOffset UtcEndExclusive(DateOnly date) =>
        date == DateOnly.MaxValue ? DateTimeOffset.MaxValue : new(date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    private static int Skip(int page, int pageSize) =>
        (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
}
