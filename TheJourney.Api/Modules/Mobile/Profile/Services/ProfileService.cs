using Microsoft.EntityFrameworkCore;
using TheJourney.Api.Infrastructure.Database;
using TheJourney.Api.Modules.Mobile.Profile.Models;

namespace TheJourney.Api.Modules.Mobile.Profile.Services;

public class ProfileService : IProfileService
{
    private readonly AppDbContext _context;
    private readonly IResumeExtractionService _resumeExtractionService;

    public ProfileService(AppDbContext context, IResumeExtractionService resumeExtractionService)
    {
        _context = context;
        _resumeExtractionService = resumeExtractionService;
    }

    public async Task<ProfileDataResult> GetProfileAsync(int studentId)
    {
        var educations = await _context.Set<StudentEducation>()
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        var skills = await _context.Set<StudentSkill>()
            .Where(s => s.StudentId == studentId)
            .OrderBy(s => s.SkillName)
            .ToListAsync();

        var experiences = await _context.Set<StudentExperience>()
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        var projects = await _context.Set<StudentProject>()
            .Where(p => p.StudentId == studentId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var languages = await _context.Set<StudentLanguage>()
            .Where(l => l.StudentId == studentId)
            .OrderBy(l => l.LanguageName)
            .ToListAsync();

        return new ProfileDataResult
        {
            Educations = educations,
            Skills = skills,
            Experiences = experiences,
            Projects = projects,
            Languages = languages
        };
    }

    public async Task<ProfileExtractionResult> ExtractAndSaveResumeAsync(int studentId, Stream pdfStream)
    {
        // Extract data from PDF
        var extractionResult = await _resumeExtractionService.ExtractFromPdfAsync(pdfStream);

        // Clear existing data for this student (optional - you might want to keep history)
        // For now, we'll replace existing data
        await ClearExistingProfileDataAsync(studentId);

        var result = new ProfileExtractionResult();

        // Save educations
        foreach (var edu in extractionResult.Educations)
        {
            var studentEducation = new StudentEducation
            {
                StudentId = studentId,
                Institution = edu.Institution,
                Degree = edu.Degree,
                FieldOfStudy = edu.FieldOfStudy,
                StartDate = edu.StartDate,
                EndDate = edu.EndDate,
                IsCurrent = edu.IsCurrent,
                Description = edu.Description,
                CreatedAt = DateTime.UtcNow
            };
            _context.Set<StudentEducation>().Add(studentEducation);
        }
        result.EducationsCount = extractionResult.Educations.Count;

        // Save skills
        foreach (var skillName in extractionResult.Skills)
        {
            var studentSkill = new StudentSkill
            {
                StudentId = studentId,
                SkillName = skillName,
                CreatedAt = DateTime.UtcNow
            };
            _context.Set<StudentSkill>().Add(studentSkill);
        }
        result.SkillsCount = extractionResult.Skills.Count;

        // Save experiences
        foreach (var exp in extractionResult.Experiences)
        {
            var studentExperience = new StudentExperience
            {
                StudentId = studentId,
                CompanyName = exp.CompanyName,
                JobTitle = exp.JobTitle,
                StartDate = exp.StartDate,
                EndDate = exp.EndDate,
                IsCurrent = exp.IsCurrent,
                Description = exp.Description,
                Location = exp.Location,
                CreatedAt = DateTime.UtcNow
            };
            _context.Set<StudentExperience>().Add(studentExperience);
        }
        result.ExperiencesCount = extractionResult.Experiences.Count;

        // Save projects
        foreach (var proj in extractionResult.Projects)
        {
            var studentProject = new StudentProject
            {
                StudentId = studentId,
                ProjectName = proj.ProjectName,
                StartDate = proj.StartDate,
                EndDate = proj.EndDate,
                Description = proj.Description,
                Technologies = proj.Technologies,
                Url = proj.Url,
                CreatedAt = DateTime.UtcNow
            };
            _context.Set<StudentProject>().Add(studentProject);
        }
        result.ProjectsCount = extractionResult.Projects.Count;

        // Save languages
        foreach (var lang in extractionResult.Languages)
        {
            var studentLanguage = new StudentLanguage
            {
                StudentId = studentId,
                LanguageName = lang.LanguageName,
                ProficiencyLevel = lang.ProficiencyLevel,
                CreatedAt = DateTime.UtcNow
            };
            _context.Set<StudentLanguage>().Add(studentLanguage);
        }
        result.LanguagesCount = extractionResult.Languages.Count;

        // Save all changes
        await _context.SaveChangesAsync();

        result.Message = $"Successfully extracted and saved: {result.EducationsCount} education(s), {result.SkillsCount} skill(s), {result.ExperiencesCount} experience(s), {result.ProjectsCount} project(s), {result.LanguagesCount} language(s).";

        return result;
    }

    private async Task ClearExistingProfileDataAsync(int studentId)
    {
        var educations = await _context.Set<StudentEducation>()
            .Where(e => e.StudentId == studentId)
            .ToListAsync();
        _context.Set<StudentEducation>().RemoveRange(educations);

        var skills = await _context.Set<StudentSkill>()
            .Where(s => s.StudentId == studentId)
            .ToListAsync();
        _context.Set<StudentSkill>().RemoveRange(skills);

        var experiences = await _context.Set<StudentExperience>()
            .Where(e => e.StudentId == studentId)
            .ToListAsync();
        _context.Set<StudentExperience>().RemoveRange(experiences);

        var projects = await _context.Set<StudentProject>()
            .Where(p => p.StudentId == studentId)
            .ToListAsync();
        _context.Set<StudentProject>().RemoveRange(projects);

        var languages = await _context.Set<StudentLanguage>()
            .Where(l => l.StudentId == studentId)
            .ToListAsync();
        _context.Set<StudentLanguage>().RemoveRange(languages);

        await _context.SaveChangesAsync();
    }
}

