using System.ComponentModel.DataAnnotations;
using TheJourney.Api.Modules.Mobile.Auth.Models;

namespace TheJourney.Api.Modules.Mobile.Profile.Models;

public class StudentLanguage
{
    public int Id { get; set; }
    
    [Required]
    public int StudentId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string LanguageName { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? ProficiencyLevel { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation property
    public Student? Student { get; set; }
}

