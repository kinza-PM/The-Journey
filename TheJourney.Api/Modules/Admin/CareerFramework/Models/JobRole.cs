using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace TheJourney.Api.Modules.Admin.CareerFramework.Models;

public class JobRole
{
    public int Id { get; set; }

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

    public int MajorId { get; set; }
    [JsonIgnore]
    public Major? Major { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int? CreatedByAdminId { get; set; }
    public global::TheJourney.Api.Modules.Admin.Auth.Models.Admin? CreatedByAdmin { get; set; }

    [JsonIgnore]
    public ICollection<AssessmentTemplate> AssessmentTemplates { get; set; } = new List<AssessmentTemplate>();
}

