using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using MunicipalElections.Dtos;
using MunicipalElections.Models;
using MunicipalElections.Services;

namespace MunicipalElections.Controllers.Api;

[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    JwtTokenService tokenService) : ApiControllerBase
{
    // POST /api/auth/register
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            ModelState.AddModelError(nameof(request.Email), "An account with this email already exists.");
            return ValidationProblem(ModelState);
        }

        // Mobile app users sign in with their email, so it doubles as the user name.
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            Name = request.Name,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var key = error.Code.StartsWith("Password") ? nameof(request.Password) : nameof(request.Email);
                ModelState.AddModelError(key, error.Description);
            }
            return ValidationProblem(ModelState);
        }

        return StatusCode(StatusCodes.Status201Created, CreateAuthResponse(user));
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Problem(title: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return Problem(title: "Account is locked. Try again later.", statusCode: StatusCodes.Status401Unauthorized);
        }
        if (!result.Succeeded)
        {
            return Problem(title: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);
        }

        return Ok(CreateAuthResponse(user));
    }

    // GET /api/auth/me
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var user = await userManager.FindByIdAsync(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? string.Empty);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new UserDto(user.Id, user.Name, user.Email));
    }

    private AuthResponse CreateAuthResponse(ApplicationUser user)
    {
        var (token, expiresAt) = tokenService.CreateToken(user);
        return new AuthResponse(token, ToUtcSeconds(expiresAt), new UserDto(user.Id, user.Name, user.Email));
    }
}
