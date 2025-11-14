using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TheJourney.Api.Infrastructure.Database;
using TheJourney.Api.Modules.Mobile.Auth.Models;
using TheJourney.Api.Modules.Mobile.Auth.Notifications;

namespace TheJourney.Api.Modules.Mobile.Auth.Services;

public interface IMobileAuthService
{
    Task<SignupResult> SignupAsync(SignupRequest request);
    Task<VerificationResult> VerifyAsync(VerificationRequest request);
    Task<ResendResult> ResendAsync(ResendRequest request);
    Task<LoginResult> LoginAsync(LoginRequest request);
    Task<PasswordResetRequestResult> RequestPasswordResetAsync(PasswordResetRequest request);
    Task<PasswordResetResult> ResetPasswordAsync(PasswordResetConfirmRequest request);
}

public record SignupRequest(string Email, string Password, string? FullName);
public record SignupResult(bool Success, string Message, bool VerificationRequired, string? DebugCode = null, int? ExpiresInSeconds = null);

public record VerificationRequest(string Email, string Code);
public record VerificationResult(bool Success, string Message);

public record ResendRequest(string Email);
public record ResendResult(bool Success, string Message, string? DebugCode = null, int? ExpiresInSeconds = null);

public record LoginRequest(string Email, string Password);
public record LoginResult(bool Success, string Message, string? Token = null, DateTime? ExpiresAt = null);

public record PasswordResetRequest(string Email);
public record PasswordResetRequestResult(bool Success, string Message, string? DebugCode = null, int? ExpiresInSeconds = null);

public record PasswordResetConfirmRequest(string Email, string OtpCode, string NewPassword, string ConfirmPassword);
public record PasswordResetResult(bool Success, string Message);

public class MobileAuthService : IMobileAuthService
{
    private const string PurposeSignup = "SIGNUP";
    private const string PurposePasswordReset = "PASSWORD_RESET";
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<MobileAuthService> _logger;

    public MobileAuthService(
        AppDbContext context,
        IConfiguration configuration,
        IHostEnvironment environment,
        IEmailSender emailSender,
        ILogger<MobileAuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _environment = environment;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<SignupResult> SignupAsync(SignupRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new SignupResult(false, "A valid email address is required.", false);
        }

        if (!ValidatePasswordComplexity(request.Password, out var passwordError))
        {
            return new SignupResult(false, passwordError, false);
        }

        if (await _context.Students.AnyAsync(s => s.Email == normalizedEmail))
        {
            return new SignupResult(false, "Email already in use.", false);
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var fullName = string.IsNullOrWhiteSpace(request.FullName)
            ? normalizedEmail
            : request.FullName.Trim();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var student = new Student
            {
                FullName = fullName,
                Email = normalizedEmail,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            var (code, expiresAt) = await CreateVerificationCodeAsync(student, normalizedEmail);
            await SendVerificationEmailAsync(normalizedEmail, code, expiresAt);

            await transaction.CommitAsync();

            var debugCode = _environment.IsProduction() ? null : code;
            var expiresIn = (int)(expiresAt - DateTime.UtcNow).TotalSeconds;

            return new SignupResult(true, "Verification code generated for email.", true, debugCode, expiresIn);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            _logger.LogError(ex, "Failed to create signup verification code.");
            return new SignupResult(false, "Unable to send verification code. Try again shortly.", false);
        }
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new LoginResult(false, "A valid email address is required.");
        }

        var student = await FindStudentAsync(normalizedEmail);
        if (student == null)
        {
            return new LoginResult(false, "Invalid credentials.");
        }

        var unlocked = UnlockIfLockExpired(student);
        if (unlocked)
        {
            await _context.SaveChangesAsync();
        }

        if (student.IsLocked && student.LockUntil.HasValue && student.LockUntil.Value > DateTime.UtcNow)
        {
            var minutesRemaining = (int)Math.Ceiling((student.LockUntil.Value - DateTime.UtcNow).TotalMinutes);
            return new LoginResult(false, $"Account locked. Try again in {minutesRemaining} minute(s).");
        }

        if (!student.IsEmailVerified)
        {
            return new LoginResult(false, "Email address is not verified. Please verify before logging in.");
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, student.PasswordHash);
        if (!passwordValid)
        {
            var (maxAttempts, lockoutMinutes) = GetStudentLockoutSettings();
            student.FailedLoginAttempts++;
            student.UpdatedAt = DateTime.UtcNow;

            string message;
            if (student.FailedLoginAttempts >= maxAttempts)
            {
                student.IsLocked = true;
                student.LockUntil = DateTime.UtcNow.AddMinutes(lockoutMinutes);
                message = $"Account locked due to too many failed attempts. Try again in {lockoutMinutes} minute(s).";
            }
            else
            {
                var remaining = maxAttempts - student.FailedLoginAttempts;
                message = $"Invalid credentials. {remaining} attempt(s) remaining.";
            }

            await _context.SaveChangesAsync();
            return new LoginResult(false, message);
        }

        student.FailedLoginAttempts = 0;
        student.IsLocked = false;
        student.LockUntil = null;
        student.LastLoginAt = DateTime.UtcNow;
        student.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var (token, expiresAt) = GenerateJwtToken(student);
        return new LoginResult(true, "Login successful.", token, expiresAt);
    }

    public async Task<PasswordResetRequestResult> RequestPasswordResetAsync(PasswordResetRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new PasswordResetRequestResult(false, "A valid email address is required.");
        }

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == normalizedEmail);
        if (student == null)
        {
            return new PasswordResetRequestResult(true, "If the email is registered, a reset code has been sent.");
        }

        if (!student.IsEmailVerified)
        {
            return new PasswordResetRequestResult(false, "Email address is not verified. Verify your email before resetting the password.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            await ExpireExistingPasswordResetCodesAsync(student.Id);
            var (code, expiresAt) = await CreatePasswordResetCodeAsync(student, normalizedEmail);
            await SendPasswordResetOtpEmailAsync(normalizedEmail, code, expiresAt);

            await transaction.CommitAsync();

            var debugCode = _environment.IsProduction() ? null : code;
            var expiresInSeconds = (int)(expiresAt - DateTime.UtcNow).TotalSeconds;
            return new PasswordResetRequestResult(true, "If the email is registered, a reset code has been sent.", debugCode, expiresInSeconds);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            _logger.LogError(ex, "Failed to send password reset OTP to {Email}", normalizedEmail);
            return new PasswordResetRequestResult(false, "Unable to send reset code. Try again shortly.");
        }
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(PasswordResetConfirmRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new PasswordResetResult(false, "A valid email address is required.");
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return new PasswordResetResult(false, "New password and confirm password do not match.");
        }

        if (!ValidatePasswordComplexity(request.NewPassword, out var passwordError))
        {
            return new PasswordResetResult(false, passwordError);
        }

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == normalizedEmail);
        if (student == null)
        {
            return new PasswordResetResult(false, "Invalid or expired reset code.");
        }

        var codeEntity = await _context.VerificationCodes
            .Where(c => c.StudentId == student.Id 
                && c.Purpose == PurposePasswordReset 
                && c.Channel == "EMAIL" 
                && c.ConsumedAt == null 
                && c.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (codeEntity == null || !BCrypt.Net.BCrypt.Verify(request.OtpCode, codeEntity.CodeHash))
        {
            return new PasswordResetResult(false, "Invalid or expired reset code.");
        }

        if (codeEntity.ExpiresAt < DateTime.UtcNow)
        {
            return new PasswordResetResult(false, "Reset code has expired. Please request a new one.");
        }

        student.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        student.FailedLoginAttempts = 0;
        student.IsLocked = false;
        student.LockUntil = null;
        student.UpdatedAt = DateTime.UtcNow;

        codeEntity.ConsumedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new PasswordResetResult(true, "Password has been reset successfully.");
    }

    public async Task<VerificationResult> VerifyAsync(VerificationRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new VerificationResult(false, "A valid email address is required.");
        }

        var student = await FindStudentAsync(normalizedEmail);
        if (student == null)
        {
            return new VerificationResult(false, "Account not found.");
        }

        var code = await GetActiveVerificationCodeAsync(student.Id);
        if (code == null)
        {
            return new VerificationResult(false, "No active verification code. Request a new one.");
        }

        if (code.ExpiresAt < DateTime.UtcNow)
        {
            return new VerificationResult(false, "Verification code expired. Request a new one.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Code, code.CodeHash))
        {
            return new VerificationResult(false, "Invalid verification code.");
        }

        code.ConsumedAt = DateTime.UtcNow;

        student.IsEmailVerified = true;

        student.VerifiedAt ??= DateTime.UtcNow;

        student.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new VerificationResult(true, "Account verified.");
    }

    public async Task<ResendResult> ResendAsync(ResendRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new ResendResult(false, "A valid email address is required.");
        }

        var student = await FindStudentAsync(normalizedEmail);
        if (student == null)
        {
            return new ResendResult(false, "Account not found.");
        }

        if (student.IsEmailVerified)
        {
            return new ResendResult(false, "Account already verified.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            await ExpireExistingCodesAsync(student.Id);
            var (code, expiresAt) = await CreateVerificationCodeAsync(student, normalizedEmail);
            await SendVerificationEmailAsync(normalizedEmail, code, expiresAt);

            await transaction.CommitAsync();

            var debugCode = _environment.IsProduction() ? null : code;
            var expiresIn = (int)(expiresAt - DateTime.UtcNow).TotalSeconds;

            return new ResendResult(true, "Verification code regenerated.", debugCode, expiresIn);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            _logger.LogError(ex, "Failed to resend verification code.");
            return new ResendResult(false, "Unable to resend verification code. Try again shortly.");
        }
    }

    private async Task<(string Code, DateTime ExpiresAt)> CreateVerificationCodeAsync(Student student, string emailTarget)
    {
        var expiryMinutes = int.TryParse(_configuration["OTP_EXPIRY_MINUTES"], out var value) ? value : 10;
        var code = GenerateOtp();
        var codeHash = BCrypt.Net.BCrypt.HashPassword(code);

        var entity = new VerificationCode
        {
            StudentId = student.Id,
            Channel = "EMAIL",
            Purpose = PurposeSignup,
            CodeHash = codeHash,
            DeliveryTarget = emailTarget,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };

        _context.VerificationCodes.Add(entity);
        await _context.SaveChangesAsync();

        return (code, entity.ExpiresAt);
    }

    private async Task<(string Code, DateTime ExpiresAt)> CreatePasswordResetCodeAsync(Student student, string email)
    {
        var expiryMinutes = int.TryParse(_configuration["PASSWORD_RESET_EXPIRY_MINUTES"], out var value) ? value : 10;
        var code = GenerateOtp();
        var codeHash = BCrypt.Net.BCrypt.HashPassword(code);

        var entity = new VerificationCode
        {
            StudentId = student.Id,
            Channel = "EMAIL",
            Purpose = PurposePasswordReset,
            CodeHash = codeHash,
            DeliveryTarget = email,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };

        _context.VerificationCodes.Add(entity);
        await _context.SaveChangesAsync();

        return (code, entity.ExpiresAt);
    }

    private async Task ExpireExistingCodesAsync(int studentId)
    {
        var codes = await _context.VerificationCodes
            .Where(c => c.StudentId == studentId && c.Channel == "EMAIL" && c.Purpose == PurposeSignup && c.ConsumedAt == null && c.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        if (codes.Count == 0)
        {
            return;
        }

        foreach (var code in codes)
        {
            code.ExpiresAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    private async Task ExpireExistingPasswordResetCodesAsync(int studentId)
    {
        var codes = await _context.VerificationCodes
            .Where(c => c.StudentId == studentId && c.Purpose == PurposePasswordReset && c.ConsumedAt == null && c.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        if (codes.Count == 0)
        {
            return;
        }

        foreach (var code in codes)
        {
            code.ExpiresAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    private async Task<VerificationCode?> GetActiveVerificationCodeAsync(int studentId)
    {
        return await _context.VerificationCodes
            .Where(c => c.StudentId == studentId && c.Channel == "EMAIL" && c.Purpose == PurposeSignup && c.ConsumedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();
    }

    private bool UnlockIfLockExpired(Student student)
    {
        var unlocked = false;
        if (student.IsLocked && student.LockUntil.HasValue && student.LockUntil.Value <= DateTime.UtcNow)
        {
            student.IsLocked = false;
            student.LockUntil = null;
            student.FailedLoginAttempts = 0;
            unlocked = true;
        }

        return unlocked;
    }

    private (int MaxAttempts, int LockoutMinutes) GetStudentLockoutSettings()
    {
        var maxAttempts = int.TryParse(_configuration["STUDENT_LOCKOUT_MAX_ATTEMPTS"], out var attempts)
            ? attempts
            : int.TryParse(_configuration["LOCKOUT_MAX_ATTEMPTS"], out var fallbackAttempts) ? fallbackAttempts : 5;

        var lockoutMinutes = int.TryParse(_configuration["STUDENT_LOCKOUT_MINUTES"], out var minutes)
            ? minutes
            : int.TryParse(_configuration["LOCKOUT_MINUTES"], out var fallbackMinutes) ? fallbackMinutes : 15;

        return (maxAttempts, lockoutMinutes);
    }

    private (string Token, DateTime ExpiresAt) GenerateJwtToken(Student student)
    {
        var jwtSecret = _configuration["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET is not configured");
        var jwtIssuer = _configuration["JWT_ISSUER"] ?? throw new InvalidOperationException("JWT_ISSUER is not configured");
        var jwtAudience = _configuration["JWT_AUDIENCE"] ?? throw new InvalidOperationException("JWT_AUDIENCE is not configured");
        var tokenMinutes = int.TryParse(_configuration["STUDENT_JWT_EXPIRY_MINUTES"], out var minutes) ? minutes : 60;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, student.Id.ToString()),
            new Claim("studentId", student.Id.ToString()),
            new Claim("fullName", student.FullName)
        };

        if (!string.IsNullOrWhiteSpace(student.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, student.Email));
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(tokenMinutes);
        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }


    private async Task SendVerificationEmailAsync(string email, string code, DateTime expiresAt)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling((expiresAt - DateTime.UtcNow).TotalMinutes));
        var message = $"Your Journey verification code is {code}. It expires in {minutes} minute(s).";

        await _emailSender.SendAsync(email, "Verify your Journey account", message);
    }

    private async Task SendPasswordResetOtpEmailAsync(string email, string code, DateTime expiresAt)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling((expiresAt - DateTime.UtcNow).TotalMinutes));
        var message = $"Your Journey password reset code is {code}. It expires in {minutes} minute(s). If you did not request this, please ignore this email.";
        await _emailSender.SendAsync(email, "Reset your Journey password", message);
    }

    private async Task<Student?> FindStudentAsync(string email)
    {
        return await _context.Students.FirstOrDefaultAsync(s => s.Email == email);
    }

    private static string? NormalizeEmail(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    private bool ValidatePasswordComplexity(string password, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            errorMessage = "Password must be at least 8 characters long.";
            return false;
        }

        if (!password.Any(char.IsUpper))
        {
            errorMessage = "Password must contain at least one uppercase letter.";
            return false;
        }

        if (!Regex.IsMatch(password, @"[\W_]"))
        {
            errorMessage = "Password must contain at least one special character.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static string GenerateOtp()
    {
        const string digits = "0123456789";
        Span<char> buffer = stackalloc char[6];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        }
        return new string(buffer);
    }
}

