using System.ComponentModel.DataAnnotations;

namespace TheJourney.Api.Modules.Mobile.Assessment.Dtos;

public class StartAssessmentRequestDto
{
    [Required]
    public int IndustryId { get; set; }

    [Required]
    public int MajorId { get; set; }

    [Required]
    public int JobRoleId { get; set; }
}

public class AssessmentAnswerRequestDto
{
    [Required]
    public List<SkillAnswerDto> SkillAnswers { get; set; } = new();

    public List<QuestionAnswerDto> QuestionAnswers { get; set; } = new();
}

public class SkillAnswerDto
{
    [Required]
    public int SkillId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ProficiencyLevel { get; set; } = string.Empty;
}

public class QuestionAnswerDto
{
    [Required]
    public int QuestionId { get; set; }

    [Required]
    public string AnswerText { get; set; } = string.Empty;
}

