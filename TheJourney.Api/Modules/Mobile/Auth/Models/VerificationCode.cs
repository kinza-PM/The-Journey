using System.ComponentModel.DataAnnotations;

namespace TheJourney.Api.Modules.Mobile.Auth.Models;

public class VerificationCode
{
    public int Id { get; set; }
    public int StudentId { get; set; }

    [Required]
    [MaxLength(12)]
    public string Purpose { get; set; } = "SIGNUP";

    [Required]
    [MaxLength(20)]
    public string Channel { get; set; } = string.Empty;

    [Required]
    public string CodeHash { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? DeliveryTarget { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConsumedAt { get; set; }

    public Student Student { get; set; } = null!;
}

