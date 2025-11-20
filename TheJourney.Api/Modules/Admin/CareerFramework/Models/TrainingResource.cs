using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Models;

public class TrainingResource
{
    public int Id { get; set; }

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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public ICollection<AssessmentTemplateSkillTraining> TrainingMappings { get; set; } = new List<AssessmentTemplateSkillTraining>();
}

