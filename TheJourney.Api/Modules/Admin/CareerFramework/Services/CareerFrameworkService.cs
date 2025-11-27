using Microsoft.EntityFrameworkCore;
using TheJourney.Api.Infrastructure.Database;
using TheJourney.Api.Modules.Admin.CareerFramework.Dtos;
using TheJourney.Api.Modules.Admin.CareerFramework.Models;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Services;

public interface ICareerFrameworkService
{
    Task<List<Industry>> GetIndustriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Industry?> GetIndustryByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Industry> CreateIndustryAsync(IndustryRequestDto request, int? adminId, CancellationToken cancellationToken = default);
    Task<Industry?> UpdateIndustryAsync(int id, IndustryRequestDto request, int? adminId, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteIndustryAsync(int id, CancellationToken cancellationToken = default);

    Task<List<Major>> GetMajorsByIndustryAsync(int industryId, CancellationToken cancellationToken = default);
    Task<Major?> GetMajorByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Major> CreateMajorAsync(int industryId, MajorRequestDto request, int? adminId, CancellationToken cancellationToken = default);
    Task<Major?> UpdateMajorAsync(int id, MajorRequestDto request, int? adminId, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteMajorAsync(int id, CancellationToken cancellationToken = default);

    Task<List<JobRole>> GetJobRolesByMajorAsync(int majorId, CancellationToken cancellationToken = default);
    Task<JobRole?> GetJobRoleByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<JobRole> CreateJobRoleAsync(int majorId, JobRoleRequestDto request, int? adminId, CancellationToken cancellationToken = default);
    Task<JobRole?> UpdateJobRoleAsync(int id, JobRoleRequestDto request, int? adminId, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteJobRoleAsync(int id, CancellationToken cancellationToken = default);

    Task<List<AssessmentTemplate>> GetTemplatesByJobRoleAsync(int jobRoleId, CancellationToken cancellationToken = default);
    Task<AssessmentTemplate?> GetTemplateByIdAsync(int templateId, CancellationToken cancellationToken = default);
    Task<AssessmentTemplate> CreateTemplateAsync(int jobRoleId, AssessmentTemplateRequestDto request, int? adminId, CancellationToken cancellationToken = default);

    Task<List<Skill>> GetSkillsAsync(CancellationToken cancellationToken = default);
    Task<Skill> CreateSkillAsync(SkillRequestDto request, CancellationToken cancellationToken = default);

    Task<List<TrainingResource>> GetTrainingResourcesAsync(CancellationToken cancellationToken = default);
    Task<TrainingResource> CreateTrainingResourceAsync(TrainingResourceRequestDto request, CancellationToken cancellationToken = default);
}

public class CareerFrameworkService : ICareerFrameworkService
{
    private readonly AppDbContext _context;

    public CareerFrameworkService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Industry>> GetIndustriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        return await _context.Industries
            .AsNoTracking()
            .Where(i => includeInactive || i.IsActive)
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Industry?> GetIndustryByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Industries
            .AsNoTracking()
            .Include(i => i.Majors.Where(m => m.IsActive))
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<Industry> CreateIndustryAsync(IndustryRequestDto request, int? adminId, CancellationToken cancellationToken = default)
    {
        if (await _context.Industries.AnyAsync(i => i.Name.ToLower() == request.Name.ToLower(), cancellationToken))
        {
            throw new InvalidOperationException("Industry with the same name already exists.");
        }

        var industry = new Industry
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedByAdminId = adminId
        };

        _context.Industries.Add(industry);
        await _context.SaveChangesAsync(cancellationToken);
        return industry;
    }

    public async Task<Industry?> UpdateIndustryAsync(int id, IndustryRequestDto request, int? adminId, CancellationToken cancellationToken = default)
    {
        var industry = await _context.Industries.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (industry == null)
        {
            return null;
        }

        if (!string.Equals(industry.Name, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _context.Industries.AnyAsync(i => i.Id != id && i.Name.ToLower() == request.Name.ToLower(), cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("Another industry with the same name already exists.");
            }
        }

        industry.Name = request.Name.Trim();
        industry.Description = request.Description?.Trim();
        industry.IsActive = request.IsActive;
        industry.UpdatedAt = DateTime.UtcNow;
        industry.CreatedByAdminId ??= adminId;

        await _context.SaveChangesAsync(cancellationToken);
        return industry;
    }

    public async Task<bool> SoftDeleteIndustryAsync(int id, CancellationToken cancellationToken = default)
    {
        var industry = await _context.Industries.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (industry == null)
        {
            return false;
        }

        industry.IsActive = false;
        industry.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<Major>> GetMajorsByIndustryAsync(int industryId, CancellationToken cancellationToken = default)
    {
        return await _context.Majors
            .AsNoTracking()
            .Where(m => m.IndustryId == industryId && m.IsActive)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Major?> GetMajorByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Majors
            .AsNoTracking()
            .Include(m => m.JobRoles.Where(r => r.IsActive))
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<Major> CreateMajorAsync(int industryId, MajorRequestDto request, int? adminId, CancellationToken cancellationToken = default)
    {
        var industryExists = await _context.Industries.AnyAsync(i => i.Id == industryId, cancellationToken);
        if (!industryExists)
        {
            throw new InvalidOperationException("Industry not found.");
        }

        if (await _context.Majors.AnyAsync(m => m.IndustryId == industryId && m.Name.ToLower() == request.Name.ToLower(), cancellationToken))
        {
            throw new InvalidOperationException("Major with the same name already exists for this industry.");
        }

        var major = new Major
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IndustryId = industryId,
            CreatedAt = DateTime.UtcNow,
            CreatedByAdminId = adminId,
            IsActive = request.IsActive
        };

        _context.Majors.Add(major);
        await _context.SaveChangesAsync(cancellationToken);
        return major;
    }

    public async Task<Major?> UpdateMajorAsync(int id, MajorRequestDto request, int? adminId, CancellationToken cancellationToken = default)
    {
        var major = await _context.Majors.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (major == null)
        {
            return null;
        }

        if (!string.Equals(major.Name, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _context.Majors.AnyAsync(m =>
                m.Id != id && m.IndustryId == major.IndustryId && m.Name.ToLower() == request.Name.ToLower(), cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("Another major with the same name already exists in this industry.");
            }
        }

        major.Name = request.Name.Trim();
        major.Description = request.Description?.Trim();
        major.IsActive = request.IsActive;
        major.UpdatedAt = DateTime.UtcNow;
        major.CreatedByAdminId ??= adminId;

        await _context.SaveChangesAsync(cancellationToken);
        return major;
    }

    public async Task<bool> SoftDeleteMajorAsync(int id, CancellationToken cancellationToken = default)
    {
        var major = await _context.Majors.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (major == null)
        {
            return false;
        }

        major.IsActive = false;
        major.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<JobRole>> GetJobRolesByMajorAsync(int majorId, CancellationToken cancellationToken = default)
    {
        return await _context.JobRoles
            .AsNoTracking()
            .Where(r => r.MajorId == majorId && r.IsActive)
            .OrderBy(r => r.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<JobRole?> GetJobRoleByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.JobRoles
            .AsNoTracking()
            .Include(r => r.AssessmentTemplates.OrderByDescending(t => t.Version))
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<JobRole> CreateJobRoleAsync(int majorId, JobRoleRequestDto request, int? adminId, CancellationToken cancellationToken = default)
    {
        var majorExists = await _context.Majors.AnyAsync(m => m.Id == majorId, cancellationToken);
        if (!majorExists)
        {
            throw new InvalidOperationException("Major not found.");
        }

        if (await _context.JobRoles.AnyAsync(r => r.MajorId == majorId && r.Title.ToLower() == request.Title.ToLower(), cancellationToken))
        {
            throw new InvalidOperationException("Job role with the same title already exists for this major.");
        }

        var role = new JobRole
        {
            Title = request.Title.Trim(),
            ShortDescription = request.ShortDescription?.Trim(),
            FullDescription = request.FullDescription,
            TasksResponsibilities = request.TasksResponsibilities,
            ToolsUsed = request.ToolsUsed,
            RequiredQualification = request.RequiredQualification?.Trim(),
            MajorId = majorId,
            CreatedAt = DateTime.UtcNow,
            CreatedByAdminId = adminId,
            IsActive = request.IsActive
        };

        _context.JobRoles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);
        return role;
    }

    public async Task<JobRole?> UpdateJobRoleAsync(int id, JobRoleRequestDto request, int? adminId, CancellationToken cancellationToken = default)
    {
        var role = await _context.JobRoles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role == null)
        {
            return null;
        }

        if (!string.Equals(role.Title, request.Title, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _context.JobRoles.AnyAsync(r =>
                r.Id != id && r.MajorId == role.MajorId && r.Title.ToLower() == request.Title.ToLower(), cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("Another job role with the same title already exists for this major.");
            }
        }

        role.Title = request.Title.Trim();
        role.ShortDescription = request.ShortDescription?.Trim();
        role.FullDescription = request.FullDescription;
        role.TasksResponsibilities = request.TasksResponsibilities;
        role.ToolsUsed = request.ToolsUsed;
        role.RequiredQualification = request.RequiredQualification?.Trim();
        role.IsActive = request.IsActive;
        role.UpdatedAt = DateTime.UtcNow;
        role.CreatedByAdminId ??= adminId;

        await _context.SaveChangesAsync(cancellationToken);
        return role;
    }

    public async Task<bool> SoftDeleteJobRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = await _context.JobRoles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role == null)
        {
            return false;
        }

        role.IsActive = false;
        role.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<AssessmentTemplate>> GetTemplatesByJobRoleAsync(int jobRoleId, CancellationToken cancellationToken = default)
    {
        return await _context.AssessmentTemplates
            .AsNoTracking()
            .Include(t => t.SkillMatrix)
                .ThenInclude(sm => sm.Skill)
            .Include(t => t.Questions)
            .Include(t => t.TrainingMappings)
                .ThenInclude(tm => tm.TrainingResource)
            .Where(t => t.JobRoleId == jobRoleId)
            .OrderByDescending(t => t.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<AssessmentTemplate?> GetTemplateByIdAsync(int templateId, CancellationToken cancellationToken = default)
    {
        return await _context.AssessmentTemplates
            .AsNoTracking()
            .Include(t => t.SkillMatrix).ThenInclude(sm => sm.Skill)
            .Include(t => t.Questions)
            .Include(t => t.TrainingMappings).ThenInclude(tm => tm.TrainingResource)
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);
    }

    public async Task<AssessmentTemplate> CreateTemplateAsync(int jobRoleId, AssessmentTemplateRequestDto request, int? adminId, CancellationToken cancellationToken = default)
    {
        var roleExists = await _context.JobRoles.AnyAsync(r => r.Id == jobRoleId, cancellationToken);
        if (!roleExists)
        {
            throw new InvalidOperationException("Job role not found.");
        }

        if (request.Skills == null || request.Skills.Count == 0)
        {
            throw new InvalidOperationException("At least one skill is required to create a template.");
        }

        var skillIds = request.Skills.Select(s => s.SkillId).Distinct().ToList();
        var existingSkills = await _context.Skills
            .Where(s => skillIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (existingSkills.Count != skillIds.Count)
        {
            throw new InvalidOperationException("One or more provided skills do not exist.");
        }

        if (request.TrainingMappings?.Any() == true)
        {
            var trainingIds = request.TrainingMappings.Select(t => t.TrainingResourceId).Distinct().ToList();
            var existingTrainingIds = await _context.TrainingResources
                .Where(t => trainingIds.Contains(t.Id))
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            if (existingTrainingIds.Count != trainingIds.Count)
            {
                throw new InvalidOperationException("One or more provided training resources do not exist.");
            }
        }

        var latestVersion = await _context.AssessmentTemplates
            .Where(t => t.JobRoleId == jobRoleId)
            .MaxAsync(t => (int?)t.Version, cancellationToken) ?? 0;

        var template = new AssessmentTemplate
        {
            JobRoleId = jobRoleId,
            Version = latestVersion + 1,
            CreatedAt = DateTime.UtcNow,
            CreatedByAdminId = adminId,
            SkillMatrix = request.Skills.Select(s => new AssessmentTemplateSkill
            {
                SkillId = s.SkillId,
                RequiredProficiencyLevel = s.RequiredProficiencyLevel,
                Weight = s.Weight,
                IsRequired = s.IsRequired
            }).ToList(),
            Questions = request.Questions?.Select(q => new RoleSpecificQuestion
            {
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType ?? "Technical",
                OrderIndex = q.OrderIndex,
                IsRequired = q.IsRequired
            }).ToList() ?? new List<RoleSpecificQuestion>()
        };

        _context.AssessmentTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);

        if (request.TrainingMappings?.Any() == true)
        {
            var templateSkills = template.SkillMatrix.ToDictionary(sm => sm.SkillId, sm => sm.Id);
            var trainingEntities = request.TrainingMappings
                .Where(tm => templateSkills.ContainsKey(tm.SkillId))
                .Select(tm => new AssessmentTemplateSkillTraining
                {
                    AssessmentTemplateSkillId = templateSkills[tm.SkillId],
                    TrainingResourceId = tm.TrainingResourceId,
                    Priority = tm.Priority
                })
                .ToList();

            if (trainingEntities.Count > 0)
            {
                _context.AssessmentTemplateSkillTrainings.AddRange(trainingEntities);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return template;
    }

    public async Task<List<Skill>> GetSkillsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Skills
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Skill> CreateSkillAsync(SkillRequestDto request, CancellationToken cancellationToken = default)
    {
        if (await _context.Skills.AnyAsync(s => s.Name.ToLower() == request.Name.ToLower(), cancellationToken))
        {
            throw new InvalidOperationException("Skill with the same name already exists.");
        }

        var skill = new Skill
        {
            Name = request.Name.Trim(),
            Category = request.Category?.Trim() ?? "Hard",
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Skills.Add(skill);
        await _context.SaveChangesAsync(cancellationToken);
        return skill;
    }

    public async Task<List<TrainingResource>> GetTrainingResourcesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.TrainingResources
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<TrainingResource> CreateTrainingResourceAsync(TrainingResourceRequestDto request, CancellationToken cancellationToken = default)
    {
        var resource = new TrainingResource
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Duration = request.Duration?.Trim(),
            ResourceType = request.ResourceType?.Trim() ?? "Course",
            ExternalUrl = request.ExternalUrl?.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.TrainingResources.Add(resource);
        await _context.SaveChangesAsync(cancellationToken);
        return resource;
    }
}

