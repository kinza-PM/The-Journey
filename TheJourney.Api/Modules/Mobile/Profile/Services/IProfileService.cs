using TheJourney.Api.Modules.Mobile.Profile.Models;
using TheJourney.Api.Modules.Mobile.Profile.Services;

namespace TheJourney.Api.Modules.Mobile.Profile.Services;

public interface IProfileService
{
    Task<ProfileExtractionResult> ExtractAndSaveResumeAsync(int studentId, Stream pdfStream);
    Task<ProfileDataResult> GetProfileAsync(int studentId);
}

public class ProfileExtractionResult
{
    public int EducationsCount { get; set; }
    public int SkillsCount { get; set; }
    public int ExperiencesCount { get; set; }
    public int ProjectsCount { get; set; }
    public int LanguagesCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ProfileDataResult
{
    public List<StudentEducation> Educations { get; set; } = new();
    public List<StudentSkill> Skills { get; set; } = new();
    public List<StudentExperience> Experiences { get; set; } = new();
    public List<StudentProject> Projects { get; set; } = new();
    public List<StudentLanguage> Languages { get; set; } = new();
}

