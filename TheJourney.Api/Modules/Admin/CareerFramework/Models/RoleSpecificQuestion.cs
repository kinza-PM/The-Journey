using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Models;

public class RoleSpecificQuestion
{
    public int Id { get; set; }

    public int AssessmentTemplateId { get; set; }
    [JsonIgnore]
    public AssessmentTemplate? AssessmentTemplate { get; set; }

    [Required]
    [MaxLength(1000)]
    public string QuestionText { get; set; } = string.Empty;

    [MaxLength(50)]
    public string QuestionType { get; set; } = "Technical";

    public int OrderIndex { get; set; }

    public bool IsRequired { get; set; } = true;
}

