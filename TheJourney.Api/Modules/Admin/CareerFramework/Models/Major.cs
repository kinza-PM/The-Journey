using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace TheJourney.Api.Modules.Admin.CareerFramework.Models;

public class Major
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int IndustryId { get; set; }
    [JsonIgnore]
    public Industry? Industry { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int? CreatedByAdminId { get; set; }
    public global::TheJourney.Api.Modules.Admin.Auth.Models.Admin? CreatedByAdmin { get; set; }

    [JsonIgnore]
    public ICollection<JobRole> JobRoles { get; set; } = new List<JobRole>();
    [JsonIgnore]
    public ICollection<MajorIndustryMapping> SuggestedIndustries { get; set; } = new List<MajorIndustryMapping>();
}

