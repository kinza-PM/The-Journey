using System.Text.Json;
using TheJourney.Api.Modules.Admin.CareerFramework.Models;
using TheJourney.Api.Modules.Mobile.Assessment.Models;

namespace TheJourney.Api.Modules.Mobile.Assessment.Services;

public interface IFitScoreCalculator
{
    Task<FitScoreResult> CalculateAsync(
        StudentAssessment assessment,
        AssessmentTemplate template,
        IReadOnlyCollection<AssessmentAnswer> answers,
        CancellationToken cancellationToken = default);
}

public class FitScoreCalculator : IFitScoreCalculator
{
    private const decimal HardSkillsWeight = 0.50m;
    private const decimal EducationWeight = 0.20m;
    private const decimal ToolsWeight = 0.15m;
    private const decimal SoftSkillsWeight = 0.15m;

    public Task<FitScoreResult> CalculateAsync(
        StudentAssessment assessment,
        AssessmentTemplate template,
        IReadOnlyCollection<AssessmentAnswer> answers,
        CancellationToken cancellationToken = default)
    {
        var result = new FitScoreResult();

        var skillAnswers = answers
            .Where(a => a.SkillId.HasValue)
            .ToDictionary(a => a.SkillId!.Value, a => a, EqualityComparer<int>.Default);

        var hardSkills = template.SkillMatrix.Where(sm => sm.Skill is null || !string.Equals(sm.Skill.Category, "Soft", StringComparison.OrdinalIgnoreCase)).ToList();
        var softSkills = template.SkillMatrix.Where(sm => sm.Skill != null && string.Equals(sm.Skill.Category, "Soft", StringComparison.OrdinalIgnoreCase)).ToList();

        var hardScore = ComputeWeightedSkillScore(hardSkills, skillAnswers);
        var softScore = softSkills.Count == 0 ? 1m : ComputeWeightedSkillScore(softSkills, skillAnswers);

        // Education and tools scores default to 0.5 and 0.4 respectively when no CV extraction is available
        // These can be enhanced later when mobile app provides this data through assessment answers
        var educationScore = ComputeEducationScore(null, template.JobRole?.Major?.Name);
        var toolScore = ComputeToolScore(null, template.JobRole?.ToolsUsed);

        result.Breakdown["HardSkills"] = Math.Round(hardScore * 100m, 2);
        result.Breakdown["SoftSkills"] = Math.Round(softScore * 100m, 2);
        result.Breakdown["Education"] = Math.Round(educationScore * 100m, 2);
        result.Breakdown["Tools"] = Math.Round(toolScore * 100m, 2);

        var totalScore =
            hardScore * HardSkillsWeight +
            educationScore * EducationWeight +
            toolScore * ToolsWeight +
            softScore * SoftSkillsWeight;

        result.TotalScore = Math.Round(totalScore * 100m, 2);
        result.Status = result.TotalScore switch
        {
            >= 80 => "Ready",
            >= 60 => "Nearly There",
            _ => "Foundational Work Needed"
        };

        PopulateStrengthsAndGaps(result, template, skillAnswers);
        PopulateTrainingRecommendations(result, template);

        return Task.FromResult(result);
    }

    private static decimal ComputeWeightedSkillScore(IEnumerable<AssessmentTemplateSkill> skills, IReadOnlyDictionary<int, AssessmentAnswer> answers)
    {
        var skillList = skills.ToList();
        if (skillList.Count == 0)
        {
            return 1m;
        }

        var totalWeight = skillList.Sum(s => s.Weight <= 0 ? 0.1m : s.Weight);
        if (totalWeight <= 0)
        {
            totalWeight = 1m;
        }

        decimal accumulated = 0;
        foreach (var skill in skillList)
        {
            var answer = answers.TryGetValue(skill.SkillId, out var value) ? value : null;
            var proficiencyScore = GetProficiencyRatio(answer?.ProficiencyLevel, skill.RequiredProficiencyLevel);
            accumulated += proficiencyScore * (skill.Weight <= 0 ? 0.1m : skill.Weight);
        }

        return Math.Clamp(accumulated / totalWeight, 0m, 1m);
    }

    private static decimal GetProficiencyRatio(string? actualLevel, string requiredLevel)
    {
        var actual = ToProficiencyScore(actualLevel);
        var required = ToProficiencyScore(requiredLevel);
        if (required == 0)
        {
            return 1m;
        }
        return Math.Clamp((decimal)actual / required, 0m, 1.2m);
    }

    private static int ToProficiencyScore(string? level)
    {
        return level?.Trim().ToLowerInvariant() switch
        {
            "basic" => 1,
            "intermediate" => 2,
            "advanced" => 3,
            "expert" => 4,
            "beginner" => 1,
            "proficient" => 3,
            _ => 0
        };
    }

    private static decimal ComputeEducationScore(string? extractedMajor, string? jobRoleMajor)
    {
        if (string.IsNullOrWhiteSpace(jobRoleMajor))
        {
            return 1m;
        }

        if (string.IsNullOrWhiteSpace(extractedMajor))
        {
            return 0.5m;
        }

        if (string.Equals(extractedMajor, jobRoleMajor, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        return 0.7m;
    }

    private static decimal ComputeToolScore(IReadOnlyCollection<string>? toolsFromCv, string? jobRoleTools)
    {
        if (string.IsNullOrWhiteSpace(jobRoleTools))
        {
            return 1m;
        }

        var requiredTools = jobRoleTools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (requiredTools.Length == 0)
        {
            return 1m;
        }

        if (toolsFromCv == null || toolsFromCv.Count == 0)
        {
            return 0.4m;
        }

        var matchCount = requiredTools.Count(tool =>
            toolsFromCv.Any(cvTool => string.Equals(cvTool, tool, StringComparison.OrdinalIgnoreCase)));

        return Math.Clamp((decimal)matchCount / requiredTools.Length, 0m, 1m);
    }

    private static void PopulateStrengthsAndGaps(FitScoreResult result, AssessmentTemplate template, IReadOnlyDictionary<int, AssessmentAnswer> answers)
    {
        foreach (var skill in template.SkillMatrix)
        {
            if (skill.Skill == null)
            {
                continue;
            }

            var answer = answers.TryGetValue(skill.SkillId, out var value) ? value : null;
            var ratio = GetProficiencyRatio(answer?.ProficiencyLevel, skill.RequiredProficiencyLevel);

            if (ratio >= 1m)
            {
                result.Strengths.Add($"{skill.Skill.Name} ({answer?.ProficiencyLevel ?? "Not Provided"})");
            }
            else
            {
                result.Gaps.Add($"{skill.Skill.Name} (Required: {skill.RequiredProficiencyLevel})");
            }
        }
    }

    private static void PopulateTrainingRecommendations(FitScoreResult result, AssessmentTemplate template)
    {
        foreach (var skill in template.SkillMatrix)
        {
            if (skill.Skill == null || skill.TrainingRecommendations == null)
            {
                continue;
            }

            foreach (var training in skill.TrainingRecommendations.OrderBy(t => t.Priority))
            {
                if (training.TrainingResource == null)
                {
                    continue;
                }

                result.TrainingRecommendations.Add(new TrainingRecommendation
                {
                    SkillName = skill.Skill.Name,
                    TrainingTitle = training.TrainingResource.Title,
                    Duration = training.TrainingResource.Duration,
                    ResourceType = training.TrainingResource.ResourceType,
                    ExternalUrl = training.TrainingResource.ExternalUrl,
                    Priority = training.Priority
                });
            }
        }
    }
}

