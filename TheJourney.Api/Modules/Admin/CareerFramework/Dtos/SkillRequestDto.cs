using System.ComponentModel.DataAnnotations;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Dtos;

public class SkillRequestDto
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Category { get; set; } = "Hard";

    [MaxLength(1000)]
    public string? Description { get; set; }
}

