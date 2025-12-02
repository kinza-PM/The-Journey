using System.ComponentModel.DataAnnotations;
using TheJourney.Api.Modules.Mobile.Auth.Models;

namespace TheJourney.Api.Modules.Mobile.Profile.Models;

public class StudentEducation
{
    public int Id { get; set; }
    
    [Required]
    public int StudentId { get; set; }
    
    [MaxLength(200)]
    public string? Institution { get; set; }
    
    [MaxLength(200)]
    public string? Degree { get; set; }
    
    [MaxLength(100)]
    public string? FieldOfStudy { get; set; }
    
    [MaxLength(50)]
    public string? StartDate { get; set; }
    
    [MaxLength(50)]
    public string? EndDate { get; set; }
    
    public bool IsCurrent { get; set; }
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation property
    public Student? Student { get; set; }
}

