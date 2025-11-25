using System.ComponentModel.DataAnnotations;
using TheJourney.Api.Modules.Mobile.Auth.Models;

namespace TheJourney.Api.Modules.Mobile.Profile.Models;

public class StudentSkill
{
    public int Id { get; set; }
    
    [Required]
    public int StudentId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string SkillName { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? ProficiencyLevel { get; set; }
    
    [MaxLength(50)]
    public string? Category { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation property
    public Student? Student { get; set; }
}

