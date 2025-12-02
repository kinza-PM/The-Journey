using System.ComponentModel.DataAnnotations;
using TheJourney.Api.Modules.Admin.CareerFramework.Models;

namespace TheJourney.Api.Modules.Mobile.Assessment.Models;

public class AssessmentAnswer
{
    public int Id { get; set; }

    public int StudentAssessmentId { get; set; }
    public StudentAssessment? StudentAssessment { get; set; }

    public int? RoleSpecificQuestionId { get; set; }
    public RoleSpecificQuestion? RoleSpecificQuestion { get; set; }

    public int? SkillId { get; set; }
    public Skill? Skill { get; set; }

    public string? AnswerText { get; set; }

    [MaxLength(50)]
    public string? ProficiencyLevel { get; set; }

    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
}

