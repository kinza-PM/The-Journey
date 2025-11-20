using System.ComponentModel.DataAnnotations;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Dtos;

public class TrainingResourceRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Duration { get; set; }

    [MaxLength(50)]
    public string ResourceType { get; set; } = "Course";

    [MaxLength(500)]
    public string? ExternalUrl { get; set; }

    public bool IsActive { get; set; } = true;
}

