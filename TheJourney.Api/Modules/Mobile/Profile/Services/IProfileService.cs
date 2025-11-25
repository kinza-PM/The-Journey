using TheJourney.Api.Modules.Mobile.Profile.Models;
using TheJourney.Api.Modules.Mobile.Profile.Services;

namespace TheJourney.Api.Modules.Mobile.Profile.Services;

public interface IProfileService
{
    Task<ProfileExtractionResult> ExtractAndSaveResumeAsync(int studentId, Stream stream, string contentType, string fileName);
    Task<ProfileDataResult> GetProfileAsync(int studentId);
    Task ImportLinkedInProfileAsync(int studentId, LinkedInProfileDto profile);
    Task<ProfileSuggestionResult> GetAiSuggestionsAsync(int studentId);
    Task UpdateProfileAsync(int studentId, ProfileUpdateDto update);
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

public class LinkedInProfileDto
{
    public string? Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? ProfilePictureUrl { get; set; }

    // Optional rich fields if available via partnership scopes
    public List<LinkedInPosition>? Positions { get; set; }
    public List<LinkedInEducation>? Educations { get; set; }
    public List<string>? Skills { get; set; }
    public List<LinkedInProject>? Projects { get; set; }
    public List<string>? Languages { get; set; }
}

public class LinkedInPosition
{
    public string? Title { get; set; }
    public string? Company { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
}

public class LinkedInEducation
{
    public string? SchoolName { get; set; }
    public string? Degree { get; set; }
    public string? FieldOfStudy { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}

public class LinkedInProject
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
}

public class ProfileSuggestionResult
{
    public string SuggestedHeadline { get; set; } = string.Empty;
    public string SuggestedSummary { get; set; } = string.Empty;
    public List<string> ExperienceSuggestions { get; set; } = new();
    public List<string> EducationSuggestions { get; set; } = new();
    public List<string> ProjectSuggestions { get; set; } = new();
    public string SkillsSuggestion { get; set; } = string.Empty;
    public string LanguagesSuggestion { get; set; } = string.Empty;
    public List<string> ActionableTips { get; set; } = new();
}

public class ProfileUpdateDto
{
    public List<StudentEducationDto>? Educations { get; set; }
    public List<StudentSkillDto>? Skills { get; set; }
    public List<StudentExperienceDto>? Experiences { get; set; }
    public List<StudentProjectDto>? Projects { get; set; }
    public List<StudentLanguageDto>? Languages { get; set; }
}

public class StudentEducationDto
{
    public int? Id { get; set; }
    public string? Institution { get; set; }
    public string? Degree { get; set; }
    public string? FieldOfStudy { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? Description { get; set; }
}

public class StudentSkillDto
{
    public int? Id { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string? ProficiencyLevel { get; set; }
    public string? Category { get; set; }
}

public class StudentExperienceDto
{
    public int? Id { get; set; }
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
}

public class StudentProjectDto
{
    public int? Id { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Description { get; set; }
    public string? Technologies { get; set; }
    public string? Url { get; set; }
}

public class StudentLanguageDto
{
    public int? Id { get; set; }
    public string LanguageName { get; set; } = string.Empty;
    public string? ProficiencyLevel { get; set; }
}


