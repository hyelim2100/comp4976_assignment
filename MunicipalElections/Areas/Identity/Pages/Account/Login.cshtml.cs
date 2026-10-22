using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MunicipalElections.Models;

namespace MunicipalElections.Areas.Identity.Pages.Account;

// Overrides the default Identity UI login so administrators sign in with a user name
// (e.g. "super", "pm") instead of an email address.
[AllowAnonymous]
public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        // Clear any existing external cookie to ensure a clean login process.
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ReturnUrl = returnUrl ?? Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Accept an email address too, for accounts created through the mobile app API.
        var userName = Input.UserName.Trim();
        if (userName.Contains('@') && await userManager.FindByEmailAsync(userName) is { UserName: not null } byEmail)
        {
            userName = byEmail.UserName;
        }

        var result = await signInManager.PasswordSignInAsync(userName, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            logger.LogInformation("User {UserName} logged in.", userName);
            return LocalRedirect(returnUrl);
        }
        if (result.IsLockedOut)
        {
            logger.LogWarning("User {UserName} account locked out.", userName);
            return RedirectToPage("./Lockout");
        }

        ModelState.AddModelError(string.Empty, "Invalid username or password.");
        return Page();
    }
}
