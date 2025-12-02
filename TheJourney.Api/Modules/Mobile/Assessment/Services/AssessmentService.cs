using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheJourney.Api.Infrastructure.Database;
using TheJourney.Api.Modules.Admin.CareerFramework.Models;
using TheJourney.Api.Modules.Mobile.Assessment.Dtos;
using TheJourney.Api.Modules.Mobile.Assessment.Models;

namespace TheJourney.Api.Modules.Mobile.Assessment.Services;

public interface IAssessmentService
{
    Task<IReadOnlyList<Industry>> GetIndustriesAsync(int studentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Major>> GetMajorsAsync(int industryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobRole>> GetJobRolesAsync(int majorId, CancellationToken cancellationToken = default);
    Task<StudentAssessment> StartAssessmentAsync(int studentId, StartAssessmentRequestDto request, CancellationToken cancellationToken = default);
    Task SaveAnswersAsync(int studentId, int assessmentId, AssessmentAnswerRequestDto request, CancellationToken cancellationToken = default);
    Task<FitScoreResult> SubmitAssessmentAsync(int studentId, int assessmentId, CancellationToken cancellationToken = default);
    Task<StudentAssessment?> GetAssessmentAsync(int studentId, int assessmentId, CancellationToken cancellationToken = default);
    Task<StudentAssessment?> GetAssessmentWithTemplateAsync(int studentId, int assessmentId, CancellationToken cancellationToken = default);
}

public class AssessmentService : IAssessmentService
{
    private readonly AppDbContext _context;
    private readonly IFitScoreCalculator _fitScoreCalculator;
    private readonly ILogger<AssessmentService> _logger;

    public AssessmentService(
        AppDbContext context,
        IFitScoreCalculator fitScoreCalculator,
        ILogger<AssessmentService> logger)
    {
        _context = context;
        _fitScoreCalculator = fitScoreCalculator;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Industry>> GetIndustriesAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var industries = await _context.Industries
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken);

        return industries;
    }

    public async Task<IReadOnlyList<Major>> GetMajorsAsync(int industryId, CancellationToken cancellationToken = default)
    {
        return await _context.Majors
            .AsNoTracking()
            .Where(m => m.IndustryId == industryId && m.IsActive)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobRole>> GetJobRolesAsync(int majorId, CancellationToken cancellationToken = default)
    {
        return await _context.JobRoles
            .AsNoTracking()
            .Where(r => r.MajorId == majorId && r.IsActive)
            .OrderBy(r => r.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<StudentAssessment> StartAssessmentAsync(int studentId, StartAssessmentRequestDto request, CancellationToken cancellationToken = default)
    {
        var jobRole = await _context.JobRoles
            .Include(r => r.Major)
                .ThenInclude(m => m!.Industry)
            .FirstOrDefaultAsync(r => r.Id == request.JobRoleId, cancellationToken);

        if (jobRole == null)
        {
            throw new InvalidOperationException("Job role not found.");
        }

        var major = jobRole.Major ?? throw new InvalidOperationException("Major not found for job role.");
        var industry = major.Industry ?? throw new InvalidOperationException("Industry not found for job role.");
        var majorId = major.Id;
        var industryId = industry.Id;

        var majorMismatch = request.MajorId != 0 && request.MajorId != majorId;
        var industryMismatch = request.IndustryId != 0 && request.IndustryId != industryId;
        if (majorMismatch || industryMismatch)
        {
            _logger.LogWarning("Student {StudentId} attempted to start assessment with mismatched hierarchy. Requested IndustryId={RequestedIndustryId}, MajorId={RequestedMajorId}, JobRoleId={JobRoleId}. Actual IndustryId={ActualIndustryId}, MajorId={ActualMajorId}. Continuing with actual hierarchy.",
                studentId,
                request.IndustryId,
                request.MajorId,
                request.JobRoleId,
                industryId,
                majorId);
        }

        var template = await _context.AssessmentTemplates
            .Include(t => t.SkillMatrix).ThenInclude(sm => sm.Skill)
            .Include(t => t.Questions)
            .Include(t => t.TrainingMappings)
                .ThenInclude(tm => tm.TrainingResource)
            .Include(t => t.JobRole)
            .Where(t => t.JobRoleId == jobRole.Id)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (template == null)
        {
            throw new InvalidOperationException("No assessment template exists for this job role.");
        }

        template.SkillMatrix ??= new List<AssessmentTemplateSkill>();
        template.Questions ??= new List<RoleSpecificQuestion>();
        template.TrainingMappings ??= new List<AssessmentTemplateSkillTraining>();

        var assessment = new StudentAssessment
        {
            StudentId = studentId,
            JobRoleId = jobRole.Id,
            AssessmentTemplateId = template.Id,
            StartedAt = DateTime.UtcNow,
            Status = "InProgress"
        };

        assessment.AssessmentTemplate = template;

        _context.StudentAssessments.Add(assessment);
        await _context.SaveChangesAsync(cancellationToken);

        return assessment;
    }

    public async Task SaveAnswersAsync(int studentId, int assessmentId, AssessmentAnswerRequestDto request, CancellationToken cancellationToken = default)
    {
        var assessment = await _context.StudentAssessments
            .Include(a => a.AssessmentTemplate!)
                .ThenInclude(t => t.SkillMatrix!)
            .Include(a => a.AssessmentTemplate!)
                .ThenInclude(t => t.Questions!)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.StudentId == studentId, cancellationToken);

        if (assessment == null)
        {
            throw new InvalidOperationException("Assessment not found.");
        }

        if (!string.Equals(assessment.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Assessment is already completed.");
        }

        var template = assessment.AssessmentTemplate ?? throw new InvalidOperationException("Template not found.");
        
        // Load skills if not already loaded
        if (template.SkillMatrix == null)
        {
            await _context.Entry(template)
                .Collection(t => t.SkillMatrix!)
                .Query()
                .Include(sm => sm.Skill)
                .LoadAsync(cancellationToken);
        }
        
        var skillMatrix = template.SkillMatrix?.ToList() ?? new List<AssessmentTemplateSkill>();
        var questions = template.Questions?.ToList() ?? new List<RoleSpecificQuestion>();
        
        // Only validate required skills
        var requiredSkillIds = skillMatrix
            .Where(sm => sm.IsRequired)
            .Select(sm => sm.SkillId)
            .ToList();

        var providedSkillIds = request.SkillAnswers.Select(sa => sa.SkillId).ToList();
        var missingSkills = requiredSkillIds.Except(providedSkillIds).ToList();
        if (missingSkills.Any())
        {
            // Get skill names for better error message
            var missingSkillDetails = skillMatrix
                .Where(sm => missingSkills.Contains(sm.SkillId))
                .Select(sm => $"SkillId {sm.SkillId} ({sm.Skill?.Name ?? "Unknown"})")
                .ToList();
            
            throw new InvalidOperationException(
                $"Skill answers must provide values for all required skills. Missing required skills: {string.Join(", ", missingSkillDetails)}. " +
                $"Required skill IDs: {string.Join(", ", requiredSkillIds)}. Provided skill IDs: {string.Join(", ", providedSkillIds)}.");
        }

        // Validate that all provided skill IDs belong to the template
        var validSkillIds = skillMatrix.Select(sm => sm.SkillId).ToHashSet();
        var invalidSkillIds = providedSkillIds.Except(validSkillIds).ToList();
        if (invalidSkillIds.Any())
        {
            throw new InvalidOperationException($"The following skill IDs do not belong to this assessment template: {string.Join(", ", invalidSkillIds)}.");
        }

        var existingAnswers = await _context.AssessmentAnswers
            .Where(a => a.StudentAssessmentId == assessment.Id)
            .ToListAsync(cancellationToken);

        _context.AssessmentAnswers.RemoveRange(existingAnswers);

        foreach (var skillAnswer in request.SkillAnswers)
        {
            _context.AssessmentAnswers.Add(new AssessmentAnswer
            {
                StudentAssessmentId = assessment.Id,
                SkillId = skillAnswer.SkillId,
                ProficiencyLevel = skillAnswer.ProficiencyLevel.Trim(),
                AnsweredAt = DateTime.UtcNow
            });
        }

        foreach (var questionAnswer in request.QuestionAnswers)
        {
            var question = questions.FirstOrDefault(q => q.Id == questionAnswer.QuestionId);
            if (question == null)
            {
                throw new InvalidOperationException($"Question {questionAnswer.QuestionId} does not belong to this template.");
            }

            _context.AssessmentAnswers.Add(new AssessmentAnswer
            {
                StudentAssessmentId = assessment.Id,
                RoleSpecificQuestionId = questionAnswer.QuestionId,
                AnswerText = questionAnswer.AnswerText.Trim(),
                AnsweredAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FitScoreResult> SubmitAssessmentAsync(int studentId, int assessmentId, CancellationToken cancellationToken = default)
    {
        var assessment = await _context.StudentAssessments
            .Include(a => a.AssessmentTemplate!)
                .ThenInclude(t => t.SkillMatrix!)
                    .ThenInclude(sm => sm.Skill)
            .Include(a => a.AssessmentTemplate!)
                .ThenInclude(t => t.TrainingMappings!)
                    .ThenInclude(tm => tm.TrainingResource)
            .Include(a => a.AssessmentTemplate!)
                .ThenInclude(t => t.JobRole!)
                    .ThenInclude(r => r.Major!)
                        .ThenInclude(m => m.Industry!)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.StudentId == studentId, cancellationToken);

        if (assessment == null)
        {
            throw new InvalidOperationException("Assessment not found.");
        }

        if (!string.Equals(assessment.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            var existingResult = assessment.FitScoreBreakdownJson != null
                ? JsonSerializer.Deserialize<FitScoreResult>(assessment.FitScoreBreakdownJson)
                : null;
            return existingResult ?? new FitScoreResult { TotalScore = assessment.FitScore ?? 0 };
        }

        var answers = await _context.AssessmentAnswers
            .Where(a => a.StudentAssessmentId == assessment.Id)
            .ToListAsync(cancellationToken);

        if (!answers.Any(a => a.SkillId.HasValue))
        {
            throw new InvalidOperationException("Please submit skill answers before finalizing the assessment.");
        }

        var template = assessment.AssessmentTemplate ?? throw new InvalidOperationException("Template not found.");
        template.SkillMatrix ??= new List<AssessmentTemplateSkill>();
        template.TrainingMappings ??= new List<AssessmentTemplateSkillTraining>();

        var fitScore = await _fitScoreCalculator.CalculateAsync(assessment, template, answers, cancellationToken);

        assessment.Status = "Completed";
        assessment.CompletedAt = DateTime.UtcNow;
        assessment.FitScore = fitScore.TotalScore;
        assessment.FitScoreBreakdownJson = JsonSerializer.Serialize(fitScore);

        await _context.SaveChangesAsync(cancellationToken);
        return fitScore;
    }

    public async Task<StudentAssessment?> GetAssessmentAsync(int studentId, int assessmentId, CancellationToken cancellationToken = default)
    {
        return await _context.StudentAssessments
            .AsNoTracking()
            .Include(a => a.JobRole!)
                .ThenInclude(r => r.Major!)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.StudentId == studentId, cancellationToken);
    }

    public async Task<StudentAssessment?> GetAssessmentWithTemplateAsync(int studentId, int assessmentId, CancellationToken cancellationToken = default)
    {
        return await _context.StudentAssessments
            .AsNoTracking()
            .Include(a => a.AssessmentTemplate!)
                .ThenInclude(t => t.SkillMatrix!)
                    .ThenInclude(sm => sm.Skill)
            .Include(a => a.AssessmentTemplate!)
                .ThenInclude(t => t.Questions!)
            .Include(a => a.JobRole!)
                .ThenInclude(r => r.Major!)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.StudentId == studentId, cancellationToken);
    }
}

