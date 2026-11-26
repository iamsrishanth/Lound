using System.Security.Claims;
using Lound.Api.Contracts;
using Lound.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lound.Api.Infrastructure;

public sealed record ApiError(string Code, string Message, IReadOnlyDictionary<string, string[]>? Errors = null);

public static class ApiResults
{
    public static IResult Error(int status, string code, string message,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        Results.Json(new ApiError(code, message, errors), statusCode: status);

    public static IResult Validation(IReadOnlyDictionary<string, string[]> errors) =>
        Error(StatusCodes.Status400BadRequest, "validation_failed", "The request is invalid.", errors);

    public static IResult IdentityErrors(IEnumerable<IdentityError> identityErrors)
    {
        var errors = identityErrors.GroupBy(x => x.Code)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Description).ToArray());
        return Validation(errors);
    }

    public static IResult NotFound(string message = "The requested record was not found.") =>
        Error(StatusCodes.Status404NotFound, "not_found", message);

    public static IResult Conflict(string message) =>
        Error(StatusCodes.Status409Conflict, "conflict", message);

    public static string? UserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public static async Task<UserDto> ToUserDtoAsync(ApplicationUser user, UserManager<ApplicationUser> users)
    {
        var roles = await users.GetRolesAsync(user);
        return new UserDto(user.Id, user.Email ?? string.Empty, user.DisplayName,
            roles.OrderBy(x => x, StringComparer.Ordinal).ToArray(), user.IsActive,
            !user.IsDirectoryRemoved, user.CreatedAt);
    }

    public static string Clean(string? value) => (value ?? string.Empty).Trim();

    public static bool IsUniqueConstraintViolation(Microsoft.EntityFrameworkCore.DbUpdateException exception) =>
        exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 };

    public static bool TryParseEnum<TEnum>(string? value, out TEnum parsed) where TEnum : struct, Enum =>
        Enum.TryParse(value, true, out parsed) && Enum.IsDefined(parsed);
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public static class Pagination
{
    public static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize <= 0 ? 25 : pageSize, 1, 100));
}
