namespace TheJourney.Api.Modules.Mobile.Auth.Models;

public class StudentPasswordResetToken
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConsumedAt { get; set; }

    public Student Student { get; set; } = null!;
}

