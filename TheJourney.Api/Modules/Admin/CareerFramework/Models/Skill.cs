using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Models;

public class Skill
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Category { get; set; } = "Hard"; // Hard / Soft

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public ICollection<AssessmentTemplateSkill> TemplateSkills { get; set; } = new List<AssessmentTemplateSkill>();
}

