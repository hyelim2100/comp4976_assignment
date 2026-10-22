using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MunicipalElections.Areas.Identity.Pages.Account;

// Public self-registration is disabled for the website: administrator accounts are seeded or
// created by a Super Admin. (Mobile app users register through POST /api/auth/register.)
[AllowAnonymous]
public class RegisterModel : PageModel
{
    public IActionResult OnGet() => NotFound();

    public IActionResult OnPost() => NotFound();
}
