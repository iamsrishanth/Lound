using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Lound.Api.Contracts;
using Lound.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Lound.Api.Security;

public sealed class TokenIssuer(SigningCredentials signingCredentials, IConfiguration configuration)
{
    public LoginResponse Create(ApplicationUser user, IReadOnlyCollection<string> roles)
    {
        var now = DateTimeOffset.UtcNow;
        var minutes = Math.Clamp(configuration.GetValue("Jwt:AccessTokenMinutes", 30), 1, 30);
        var expires = now.AddMinutes(minutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("sst", user.SecurityStamp ?? string.Empty),
            new(ClaimTypes.Name, user.DisplayName)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "Lound.Api",
            audience: configuration["Jwt:Audience"] ?? "Lound.Desktop",
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: signingCredentials);
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires,
            new UserDto(user.Id, user.Email ?? string.Empty, user.DisplayName,
                roles.OrderBy(x => x, StringComparer.Ordinal).ToArray(), user.IsActive,
                !user.IsDirectoryRemoved, user.CreatedAt));
    }
}
