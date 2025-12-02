using System.ComponentModel.DataAnnotations;

namespace TheJourney.Api.Modules.Admin.Auth.Models;

public class Admin
{
    public int Id { get; set; }
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    [Required]
    public string Role { get; set; } = string.Empty;
    
    public int FailedLoginAttempts { get; set; } = 0;
    
    public bool IsLocked { get; set; } = false;
    
    public DateTime? LockUntil { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
