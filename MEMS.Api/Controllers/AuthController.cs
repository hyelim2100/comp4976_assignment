using MEMS.Api.Infrastructure;
using MEMS.Data.Entities;
using MEMS.Data.Tenancy;
using MEMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Api.Controllers;

/// <summary>Administrator sign-in for API clients (JWT bearer tokens).</summary>
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    JwtTokenService tokens,
    MEMS.Data.MemsDbContext db,
    ILogger<AuthController> logger) : ApiControllerBase
{
    /// <summary>Signs in an administrator and returns a JWT.</summary>
    /// <remarks>
    /// User names are case-insensitive ("Super" and "super" are the same account).
    /// Send the token as <c>Authorization: Bearer {token}</c> on POST, PUT and DELETE requests.
    /// After 5 failed attempts the account is locked for 5 minutes.
    /// </remarks>
    /// <response code="200">Signed in.</response>
    /// <response code="400">User name or password missing.</response>
    /// <response code="401">Invalid user name or password (or account locked).</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByNameAsync(request.UserName.Trim());
        if (user is not null)
        {
            var check = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (check.Succeeded)
            {
                var (token, expires, role) = await tokens.CreateTokenAsync(user);
                var slug = user.MunicipalityId is int mid
                    ? await db.Municipalities.IgnoreQueryFilters().Where(m => m.Id == mid).Select(m => m.Slug).FirstOrDefaultAsync()
                    : null;
                logger.LogInformation("API login succeeded for {UserName}", user.UserName);
                return new LoginResponse(token, expires, user.UserName!, role, user.MunicipalityId, slug);
            }
            logger.LogWarning("API login failed for {UserName} (locked out: {LockedOut})", user.UserName, check.IsLockedOut);
        }
        else
        {
            logger.LogWarning("API login failed for unknown user {UserName}", request.UserName);
        }

        // Same answer for unknown user, wrong password and lockout: no account enumeration.
        return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in failed", detail: "Invalid user name or password.");
    }

    /// <summary>Returns the signed-in administrator and their tenant.</summary>
    /// <response code="200">The current administrator.</response>
    /// <response code="401">Missing or invalid token.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserDto> Me([FromServices] ITenantContext tenant) =>
        new CurrentUserDto(User.Identity!.Name!, tenant.IsSuperAdmin, tenant.MunicipalityId);
}

public sealed record CurrentUserDto(string UserName, bool IsSuperAdmin, int? MunicipalityId);
