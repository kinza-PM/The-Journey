using System.ComponentModel.DataAnnotations;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Dtos;

public class JobRoleRequestDto
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? ShortDescription { get; set; }

    public string? FullDescription { get; set; }

    public string? TasksResponsibilities { get; set; }

    public string? ToolsUsed { get; set; }

    [MaxLength(255)]
    public string? RequiredQualification { get; set; }

    public bool IsActive { get; set; } = true;
}

