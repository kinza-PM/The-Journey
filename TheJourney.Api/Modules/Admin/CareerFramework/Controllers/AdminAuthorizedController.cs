using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Controllers;

[Authorize(Policy = "AdminAccess")]
[ApiController]
public abstract class AdminAuthorizedController : ControllerBase
{
    protected int? GetAdminId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("adminId");
        if (claim == null)
        {
            return null;
        }

        return int.TryParse(claim.Value, out var adminId) ? adminId : null;
    }
}

