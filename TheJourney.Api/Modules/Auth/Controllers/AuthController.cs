using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheJourney.Api.Modules.Auth.Services;

namespace TheJourney.Api.Modules.Auth.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, [FromQuery] string? authType = "JWT")
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required" });
        }
        
        if (authType != null && authType.ToUpper() != "JWT" && authType.ToUpper() != "SESSION")
        {
            return BadRequest(new { message = "authType must be either 'JWT' or 'SESSION'" });
        }
        
        var result = await _authService.LoginAsync(request.Email, request.Password, HttpContext, authType ?? "JWT");
        
        if (!result.Success)
        {
            return Unauthorized(new { message = result.Message });
        }
        
        if (authType?.ToUpper() == "SESSION")
        {
            return Ok(new { sessionId = result.SessionId, message = result.Message });
        }
        
        return Ok(new { token = result.Token, message = result.Message });
    }
    
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { message = "Logged out successfully" });
    }
    
    [HttpGet("protected")]
    [Authorize(Policy = "RequireRole")]
    public IActionResult Protected()
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? 
                   User.FindFirst("Role")?.Value ??
                   HttpContext.Session.GetString("Role");
        
        return Ok(new { 
            message = "This is a protected endpoint. You are authenticated and authorized.",
            role = role,
            userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                     HttpContext.Session.GetString("AdminId")
        });
    }
    
    [HttpGet("superadmin-only")]
    [Authorize(Policy = "SuperAdminOnly")]
    public IActionResult SuperAdminOnly()
    {
        return Ok(new { message = "This endpoint is accessible only to SuperAdmin users." });
    }
    
    [HttpGet("admin-access")]
    [Authorize(Policy = "AdminAccess")]
    public IActionResult AdminAccess()
    {
        return Ok(new { message = "This endpoint is accessible to Admin and SuperAdmin users." });
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
