using System.ComponentModel.DataAnnotations;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Dtos;

public class AssessmentTemplateRequestDto
{
    [Required]
    public List<TemplateSkillRequestDto> Skills { get; set; } = new();

    public List<TemplateQuestionRequestDto> Questions { get; set; } = new();

    public List<TemplateTrainingRequestDto> TrainingMappings { get; set; } = new();
}

public class TemplateSkillRequestDto
{
    [Required]
    public int SkillId { get; set; }

    [Required]
    [MaxLength(50)]
    public string RequiredProficiencyLevel { get; set; } = "Basic";

    [Range(0, 1)]
    public decimal Weight { get; set; } = 0.1m;

    public bool IsRequired { get; set; } = true;
}

public class TemplateQuestionRequestDto
{
    [Required]
    [MaxLength(1000)]
    public string QuestionText { get; set; } = string.Empty;

    [MaxLength(50)]
    public string QuestionType { get; set; } = "Technical";

    public int OrderIndex { get; set; }

    public bool IsRequired { get; set; } = true;
}

public class TemplateTrainingRequestDto
{
    [Required]
    public int SkillId { get; set; }

    [Required]
    public int TrainingResourceId { get; set; }

    public int Priority { get; set; } = 1;
}

