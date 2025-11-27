using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Models;

public class AssessmentTemplateSkill
{
    public int Id { get; set; }

    public int AssessmentTemplateId { get; set; }
    [JsonIgnore]
    public AssessmentTemplate? AssessmentTemplate { get; set; }

    public int SkillId { get; set; }
    public Skill? Skill { get; set; }

    [Required]
    [MaxLength(50)]
    public string RequiredProficiencyLevel { get; set; } = "Basic";

    [Range(0, 1)]
    public decimal Weight { get; set; } = 0.1m;

    public bool IsRequired { get; set; } = true;

    public ICollection<AssessmentTemplateSkillTraining> TrainingRecommendations { get; set; } = new List<AssessmentTemplateSkillTraining>();
}

