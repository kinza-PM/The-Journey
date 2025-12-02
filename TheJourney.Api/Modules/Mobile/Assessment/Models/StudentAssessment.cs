using System.ComponentModel.DataAnnotations;
using TheJourney.Api.Modules.Admin.CareerFramework.Models;
using TheJourney.Api.Modules.Mobile.Auth.Models;

namespace TheJourney.Api.Modules.Mobile.Assessment.Models;

public class StudentAssessment
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public int JobRoleId { get; set; }
    public JobRole? JobRole { get; set; }

    public int AssessmentTemplateId { get; set; }
    public AssessmentTemplate? AssessmentTemplate { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "InProgress";

    public decimal? FitScore { get; set; }
    public string? FitScoreBreakdownJson { get; set; }

    public ICollection<AssessmentAnswer> Answers { get; set; } = new List<AssessmentAnswer>();
}

