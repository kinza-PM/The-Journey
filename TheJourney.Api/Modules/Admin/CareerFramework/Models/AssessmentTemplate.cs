using System.ComponentModel.DataAnnotations;
namespace TheJourney.Api.Modules.Admin.CareerFramework.Models;

public class AssessmentTemplate
{
    public int Id { get; set; }

    public int JobRoleId { get; set; }
    public JobRole? JobRole { get; set; }

    [Required]
    public int Version { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int? CreatedByAdminId { get; set; }
    public global::TheJourney.Api.Modules.Admin.Auth.Models.Admin? CreatedByAdmin { get; set; }

    public ICollection<AssessmentTemplateSkill> SkillMatrix { get; set; } = new List<AssessmentTemplateSkill>();
    public ICollection<RoleSpecificQuestion> Questions { get; set; } = new List<RoleSpecificQuestion>();
    public ICollection<AssessmentTemplateSkillTraining> TrainingMappings { get; set; } = new List<AssessmentTemplateSkillTraining>();
}

