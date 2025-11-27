using TheJourney.Api.Modules.Mobile.Profile.Models;

namespace TheJourney.Api.Modules.Mobile.Profile.Services;

public interface IResumeExtractionService
{
    Task<ResumeExtractionResult> ExtractFromPdfAsync(Stream pdfStream);
}

public class ResumeExtractionResult
{
    public List<EducationData> Educations { get; set; } = new();
    public List<string> Skills { get; set; } = new();
    public List<ExperienceData> Experiences { get; set; } = new();
    public List<ProjectData> Projects { get; set; } = new();
    public List<LanguageData> Languages { get; set; } = new();
}

public class EducationData
{
    public string? Institution { get; set; }
    public string? Degree { get; set; }
    public string? FieldOfStudy { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? Description { get; set; }
}

public class ExperienceData
{
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
}

public class ProjectData
{
    public string ProjectName { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Description { get; set; }
    public string? Technologies { get; set; }
    public string? Url { get; set; }
}

public class LanguageData
{
    public string LanguageName { get; set; } = string.Empty;
    public string? ProficiencyLevel { get; set; }
}

