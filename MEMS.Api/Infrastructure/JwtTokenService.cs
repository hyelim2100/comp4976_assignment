using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MEMS.Data.Entities;
using MEMS.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace MEMS.Api.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "MEMS.Api";
    public string Audience { get; set; } = "MEMS";

    // Signing key: never in source control. Supplied by the Aspire AppHost parameter "jwt-key"
    // (stored in the AppHost user-secrets) as the environment variable Jwt__Key.
    public string Key { get; set; } = string.Empty;

    public int LifetimeMinutes { get; set; } = 60;

    public SymmetricSecurityKey GetSigningKey()
    {
        if (Encoding.UTF8.GetByteCount(Key) < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 bytes. Run the API through MEMS.AppHost, which supplies it.");
        }
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
    }
}

public static class JwtClaimNames
{
    public const string Role = "role";
    public const string Name = "name";

    // Identity security stamp: lets the API reject tokens of deleted / re-assigned administrators.
    public const string SecurityStamp = "sstamp";
}

public class JwtTokenService(UserManager<ApplicationUser> userManager, Microsoft.Extensions.Options.IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public async Task<(string Token, DateTime ExpiresUtc, string Role)> CreateTokenAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.Contains(AppRoles.SuperAdmin) ? AppRoles.SuperAdmin : roles.FirstOrDefault() ?? string.Empty;
        var expires = DateTime.UtcNow.AddMinutes(_options.LifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtClaimNames.Name, user.UserName!),
            new(JwtClaimNames.SecurityStamp, user.SecurityStamp ?? string.Empty),
        };
        claims.AddRange(roles.Select(r => new Claim(JwtClaimNames.Role, r)));
        if (user.MunicipalityId is int municipalityId)
        {
            claims.Add(new Claim(MemsClaimTypes.MunicipalityId, municipalityId.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(_options.GetSigningKey(), SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires, role);
    }
}
