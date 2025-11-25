using System.ComponentModel.DataAnnotations;
using TheJourney.Api.Modules.Mobile.Auth.Models;

namespace TheJourney.Api.Modules.Mobile.Profile.Models;

public class StudentProject
{
    public int Id { get; set; }
    
    [Required]
    public int StudentId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string ProjectName { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? StartDate { get; set; }
    
    [MaxLength(50)]
    public string? EndDate { get; set; }
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [MaxLength(500)]
    public string? Technologies { get; set; }
    
    [MaxLength(500)]
    public string? Url { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation property
    public Student? Student { get; set; }
}

