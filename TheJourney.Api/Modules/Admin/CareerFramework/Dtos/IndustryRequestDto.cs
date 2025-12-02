using System.ComponentModel.DataAnnotations;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Dtos;

public class IndustryRequestDto
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

