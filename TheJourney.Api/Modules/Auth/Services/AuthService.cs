using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TheJourney.Api.Infrastructure.Database;
using TheJourney.Api.Modules.Admin.Auth.Models;
using AdminModel = TheJourney.Api.Modules.Admin.Auth.Models.Admin;

namespace TheJourney.Api.Modules.Auth.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string email, string password, HttpContext? httpContext = null, string? authType = "JWT");
    Task LogLoginAttemptAsync(string email, bool isSuccess, string? failureReason, int? adminId, HttpContext? httpContext = null);
}

public class LoginResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public AuthService(AppDbContext context, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }
    
    public async Task<LoginResult> LoginAsync(string email, string password, HttpContext? httpContext = null, string? authType = "JWT")
    {
        httpContext ??= _httpContextAccessor.HttpContext;
        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext?.Request.Headers["User-Agent"].ToString();
        
        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Email == email.ToLower());
        
        if (admin != null && string.IsNullOrWhiteSpace(admin.Role))
        {
            await LogLoginAttemptAsync(email, false, "Role not assigned", admin.Id, httpContext);
            return new LoginResult
            {
                Success = false,
                Message = "Access denied. No role assigned to this account."
            };
        }
        
        if (admin == null)
        {
            await LogLoginAttemptAsync(email, false, "Invalid email or password", null, httpContext);
            return new LoginResult
            {
                Success = false,
                Message = "Invalid email or password"
            };
        }
        
        if (admin.IsLocked && admin.LockUntil.HasValue && admin.LockUntil.Value > DateTime.UtcNow)
        {
            var minutesRemaining = (int)Math.Ceiling((admin.LockUntil.Value - DateTime.UtcNow).TotalMinutes);
            await LogLoginAttemptAsync(email, false, $"Account locked. Try again in {minutesRemaining} minute(s).", admin.Id, httpContext);
            return new LoginResult
            {
                Success = false,
                Message = $"Account is locked. Try again in {minutesRemaining} minute(s)."
            };
        }
        
        if (admin.IsLocked && admin.LockUntil.HasValue && admin.LockUntil.Value <= DateTime.UtcNow)
        {
            admin.IsLocked = false;
            admin.LockUntil = null;
            admin.FailedLoginAttempts = 0;
        }
        
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash);
        
        if (!isPasswordValid)
        {
            admin.FailedLoginAttempts++;
            
            var maxAttempts = int.Parse(_configuration["LOCKOUT_MAX_ATTEMPTS"] ?? "5");
            var lockoutMinutes = int.Parse(_configuration["LOCKOUT_MINUTES"] ?? "15");
            
            string failureReason;
            if (admin.FailedLoginAttempts >= maxAttempts)
            {
                admin.IsLocked = true;
                admin.LockUntil = DateTime.UtcNow.AddMinutes(lockoutMinutes);
                failureReason = $"Account locked due to too many failed attempts. Try again in {lockoutMinutes} minute(s).";
            }
            else
            {
                var attemptsRemaining = maxAttempts - admin.FailedLoginAttempts;
                failureReason = $"Invalid password. {attemptsRemaining} attempt(s) remaining.";
            }
            
            await _context.SaveChangesAsync();
            await LogLoginAttemptAsync(email, false, failureReason, admin.Id, httpContext);
            
            var remainingAttempts = maxAttempts - admin.FailedLoginAttempts;
            return new LoginResult
            {
                Success = false,
                Message = $"Invalid email or password. {remainingAttempts} attempt(s) remaining."
            };
        }
        
        admin.FailedLoginAttempts = 0;
        admin.IsLocked = false;
        admin.LockUntil = null;
        await _context.SaveChangesAsync();
        
        string? token = null;
        string? sessionId = null;
        
        if (authType.ToUpper() == "SESSION")
        {
            sessionId = Guid.NewGuid().ToString();
            if (httpContext != null && admin != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                    new Claim(ClaimTypes.Email, admin.Email),
                    new Claim(ClaimTypes.Role, admin.Role),
                    new Claim("adminId", admin.Id.ToString()),
                    new Claim("SessionId", sessionId)
                };
                
                var claimsIdentity = new ClaimsIdentity(claims, "Session");
                var authProperties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                };
                
                await httpContext.SignInAsync("Session", new ClaimsPrincipal(claimsIdentity), authProperties);
                
                httpContext.Session.SetString("AdminId", admin.Id.ToString());
                httpContext.Session.SetString("Email", admin.Email);
                httpContext.Session.SetString("Role", admin.Role);
                httpContext.Session.SetString("SessionId", sessionId);
            }
        }
        else
        {
            token = GenerateJwtToken(admin);
        }
        
        await LogLoginAttemptAsync(email, true, null, admin.Id, httpContext);
        
        return new LoginResult
        {
            Success = true,
            Token = token,
            SessionId = sessionId,
            Message = "Login successful"
        };
    }
    
    public async Task LogLoginAttemptAsync(string email, bool isSuccess, string? failureReason, int? adminId, HttpContext? httpContext = null)
    {
        httpContext ??= _httpContextAccessor.HttpContext;
        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext?.Request.Headers["User-Agent"].ToString();
        
        var loginAttempt = new LoginAttempt
        {
            Email = email.ToLower(),
            IsSuccess = isSuccess,
            FailureReason = failureReason,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            AdminId = adminId,
            AttemptedAt = DateTime.UtcNow
        };
        
        _context.LoginAttempts.Add(loginAttempt);
        await _context.SaveChangesAsync();
    }
    
    private string GenerateJwtToken(AdminModel admin)
    {
        if (string.IsNullOrWhiteSpace(admin.Role))
        {
            throw new InvalidOperationException("Admin must have a role assigned");
        }
        
        var jwtSecret = _configuration["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET is not configured");
        var jwtIssuer = _configuration["JWT_ISSUER"] ?? throw new InvalidOperationException("JWT_ISSUER is not configured");
        var jwtAudience = _configuration["JWT_AUDIENCE"] ?? throw new InvalidOperationException("JWT_AUDIENCE is not configured");
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new Claim(ClaimTypes.Email, admin.Email),
            new Claim(ClaimTypes.Role, admin.Role),
            new Claim("adminId", admin.Id.ToString())
        };
        
        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
