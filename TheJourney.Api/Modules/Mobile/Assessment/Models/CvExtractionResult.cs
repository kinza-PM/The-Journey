namespace TheJourney.Api.Modules.Mobile.Assessment.Models;

public class FitScoreResult
{
    public decimal TotalScore { get; set; }
    public Dictionary<string, decimal> Breakdown { get; set; } = new();
    public string Status { get; set; } = "Foundational";
    public List<string> Strengths { get; set; } = new();
    public List<string> Gaps { get; set; } = new();
    public List<TrainingRecommendation> TrainingRecommendations { get; set; } = new();
}

public class TrainingRecommendation
{
    public string SkillName { get; set; } = string.Empty;
    public string TrainingTitle { get; set; } = string.Empty;
    public string? Duration { get; set; }
    public string? ResourceType { get; set; }
    public string? ExternalUrl { get; set; }
    public int Priority { get; set; }
}

