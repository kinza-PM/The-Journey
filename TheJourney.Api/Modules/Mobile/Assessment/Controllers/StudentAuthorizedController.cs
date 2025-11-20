using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TheJourney.Api.Modules.Mobile.Assessment.Controllers;

[Authorize(AuthenticationSchemes = "JWT")]
[ApiController]
public abstract class StudentAuthorizedController : ControllerBase
{
    protected int? GetStudentId()
    {
        var claim = User.FindFirst("studentId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }
}

