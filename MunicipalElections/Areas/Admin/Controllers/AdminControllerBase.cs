using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MunicipalElections.Services;

namespace MunicipalElections.Areas.Admin.Controllers;

// Every admin page requires Super Admin or a municipality admin group. Controllers must still
// check MunicipalityAccess.CanManageAsync for the specific record being viewed or changed.
[Area("Admin")]
[Authorize(Policy = AdminPolicies.AdminAccess)]
public abstract class AdminControllerBase : Controller
{
    public const string StatusMessageKey = "StatusMessage";

    protected void SetStatusMessage(string message) => TempData[StatusMessageKey] = message;
}
