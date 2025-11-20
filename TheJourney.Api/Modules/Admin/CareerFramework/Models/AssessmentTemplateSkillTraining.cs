using System.Text.Json.Serialization;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Models;

public class AssessmentTemplateSkillTraining
{
    public int Id { get; set; }

    public int AssessmentTemplateSkillId { get; set; }
    [JsonIgnore]
    public AssessmentTemplateSkill? AssessmentTemplateSkill { get; set; }

    public int TrainingResourceId { get; set; }
    public TrainingResource? TrainingResource { get; set; }

    public int Priority { get; set; } = 1;
}

