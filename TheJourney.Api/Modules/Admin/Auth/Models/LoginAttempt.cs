using System.ComponentModel.DataAnnotations;

namespace TheJourney.Api.Modules.Admin.Auth.Models;

public class LoginAttempt
{
    public int Id { get; set; }
    
    public string Email { get; set; } = string.Empty;
    
    public bool IsSuccess { get; set; }
    
    public string? FailureReason { get; set; }
    
    public string? IpAddress { get; set; }
    
    public string? UserAgent { get; set; }
    
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    
    public int? AdminId { get; set; }
    
    public Admin? Admin { get; set; }
}

