using Microsoft.AspNetCore.Mvc;
using TheJourney.Api.Modules.Mobile.Auth.Services;

namespace TheJourney.Api.Modules.Mobile.Auth.Controllers;

[ApiController]
[Route("api/mobile/auth")]
public class MobileAuthController : ControllerBase
{
    private readonly IMobileAuthService _mobileAuthService;

    public MobileAuthController(IMobileAuthService mobileAuthService)
    {
        _mobileAuthService = mobileAuthService;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] SignupRequestDto dto)
    {
        var result = await _mobileAuthService.SignupAsync(new SignupRequest(dto.Email, dto.PhoneNumber, dto.Password, dto.FullName));
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new
        {
            message = result.Message,
            verificationRequired = result.VerificationRequired,
            expiresInSeconds = result.ExpiresInSeconds,
            code = result.DebugCode
        });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyRequestDto dto)
    {
        var result = await _mobileAuthService.VerifyAsync(new VerificationRequest(dto.Email, dto.PhoneNumber, dto.Code));
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new { message = result.Message });
    }

    [HttpPost("resend-otp")]
    public async Task<IActionResult> Resend([FromBody] ResendOtpRequestDto dto)
    {
        var result = await _mobileAuthService.ResendAsync(new ResendRequest(dto.Email, dto.PhoneNumber));
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new
        {
            message = result.Message,
            expiresInSeconds = result.ExpiresInSeconds,
            code = result.DebugCode
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var result = await _mobileAuthService.LoginAsync(new LoginRequest(dto.Email, dto.PhoneNumber, dto.Password));
        if (!result.Success)
        {
            return Unauthorized(new { message = result.Message });
        }

        return Ok(new { message = result.Message, token = result.Token, expiresAtUtc = result.ExpiresAt });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto)
    {
        var result = await _mobileAuthService.RequestPasswordResetAsync(new PasswordResetRequest(dto.Email));
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new { message = result.Message, code = result.DebugToken, expiresInSeconds = result.ExpiresInSeconds });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto)
    {
        var result = await _mobileAuthService.ResetPasswordAsync(new PasswordResetConfirmRequest(dto.Email, dto.Token, dto.NewPassword));
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new { message = result.Message });
    }
}

public record SignupRequestDto(string? Email, string? PhoneNumber, string Password, string FullName);
public record VerifyRequestDto(string? Email, string? PhoneNumber, string Code);
public record ResendOtpRequestDto(string? Email, string? PhoneNumber);
public record LoginRequestDto(string? Email, string? PhoneNumber, string Password);
public record ForgotPasswordRequestDto(string Email);
public record ResetPasswordRequestDto(string Email, string Token, string NewPassword);

