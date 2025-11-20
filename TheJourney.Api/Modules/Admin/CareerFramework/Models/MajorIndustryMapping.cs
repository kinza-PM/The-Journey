namespace TheJourney.Api.Modules.Admin.CareerFramework.Models;

public class MajorIndustryMapping
{
    public int Id { get; set; }

    public int MajorId { get; set; }
    public Major? Major { get; set; }

    public int IndustryId { get; set; }
    public Industry? Industry { get; set; }

    public int Priority { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

