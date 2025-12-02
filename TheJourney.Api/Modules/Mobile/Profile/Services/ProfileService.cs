using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TheJourney.Api.Infrastructure.Database;
using TheJourney.Api.Modules.Mobile.Profile.Models;

namespace TheJourney.Api.Modules.Mobile.Profile.Services;

public class ProfileService : IProfileService
{
    private readonly AppDbContext _context;
    private readonly IResumeExtractionService _resumeExtractionService;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;

    public ProfileService(AppDbContext context, IResumeExtractionService resumeExtractionService, IConfiguration config, IHttpClientFactory httpFactory)
    {
        _context = context;
        _resumeExtractionService = resumeExtractionService;
        _config = config;
        _httpFactory = httpFactory;
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

    public async Task ImportLinkedInProfileAsync(int studentId, LinkedInProfileDto profile)
    {
        // Update basic student data if available
        var student = await _context.Set<TheJourney.Api.Modules.Mobile.Auth.Models.Student>().FindAsync(studentId);
        if (student != null)
        {
            var fullName = ((profile.FirstName ?? string.Empty) + " " + (profile.LastName ?? string.Empty)).Trim();
            if (!string.IsNullOrWhiteSpace(fullName)) student.FullName = fullName;
            if (!string.IsNullOrWhiteSpace(profile.Email)) student.Email = profile.Email;
            student.UpdatedAt = DateTime.UtcNow;
        }

        // Map positions to experiences (best-effort if provided)
        if (profile.Positions != null && profile.Positions.Count > 0)
        {
            // Clear existing experiences for now and re-create
            var existing = await _context.Set<StudentExperience>().Where(e => e.StudentId == studentId).ToListAsync();
            _context.Set<StudentExperience>().RemoveRange(existing);

            foreach (var pos in profile.Positions)
            {
                var sExp = new StudentExperience
                {
                    StudentId = studentId,
                    CompanyName = pos.Company,
                    JobTitle = pos.Title,
                    StartDate = pos.StartDate,
                    EndDate = pos.EndDate,
                    Description = pos.Description,
                    Location = pos.Location,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Set<StudentExperience>().Add(sExp);
            }
        }

        // Map educations
        if (profile.Educations != null && profile.Educations.Count > 0)
        {
            var existing = await _context.Set<StudentEducation>().Where(e => e.StudentId == studentId).ToListAsync();
            _context.Set<StudentEducation>().RemoveRange(existing);
            foreach (var edu in profile.Educations)
            {
                var sEdu = new StudentEducation
                {
                    StudentId = studentId,
                    Institution = edu.SchoolName,
                    Degree = edu.Degree,
                    FieldOfStudy = edu.FieldOfStudy,
                    StartDate = edu.StartDate,
                    EndDate = edu.EndDate,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Set<StudentEducation>().Add(sEdu);
            }
        }

        // Map skills
        if (profile.Skills != null && profile.Skills.Count > 0)
        {
            var existing = await _context.Set<StudentSkill>().Where(s => s.StudentId == studentId).ToListAsync();
            _context.Set<StudentSkill>().RemoveRange(existing);
            foreach (var skill in profile.Skills)
            {
                var sSkill = new StudentSkill
                {
                    StudentId = studentId,
                    SkillName = skill,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Set<StudentSkill>().Add(sSkill);
            }
        }

        // Map projects
        if (profile.Projects != null && profile.Projects.Count > 0)
        {
            var existing = await _context.Set<StudentProject>().Where(p => p.StudentId == studentId).ToListAsync();
            _context.Set<StudentProject>().RemoveRange(existing);
            foreach (var proj in profile.Projects)
            {
                var sProj = new StudentProject
                {
                    StudentId = studentId,
                    ProjectName = proj.Title ?? string.Empty,
                    Description = proj.Description,
                    Url = proj.Url,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Set<StudentProject>().Add(sProj);
            }
        }

        // Map languages
        if (profile.Languages != null && profile.Languages.Count > 0)
        {
            var existing = await _context.Set<StudentLanguage>().Where(l => l.StudentId == studentId).ToListAsync();
            _context.Set<StudentLanguage>().RemoveRange(existing);
            foreach (var lang in profile.Languages)
            {
                var sLang = new StudentLanguage
                {
                    StudentId = studentId,
                    LanguageName = lang,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Set<StudentLanguage>().Add(sLang);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<ProfileSuggestionResult> GetAiSuggestionsAsync(int studentId)
    {
        var profile = await GetProfileAsync(studentId);
        var student = await _context.Set<TheJourney.Api.Modules.Mobile.Auth.Models.Student>().FindAsync(studentId);

        // If OPENAI_API_KEY provided, call OpenAI ChatCompletion for suggestions with retries
        var apiKey = _config["OPENAI_API_KEY"];
        var model = _config["OPENAI_MODEL"] ?? "gpt-3.5-turbo";
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var maxAttempts = 3;
            var attempt = 0;
            while (attempt < maxAttempts)
            {
                attempt++;
                try
                {
                    var client = _httpFactory.CreateClient();
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    client.DefaultRequestHeaders.Add("User-Agent", "TheJourney.Api/1.0");

                    // Build a strict prompt that instructs model to return ONLY valid JSON matching the schema
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("You are a resume assistant. Return ONLY a single valid JSON object (no surrounding commentary) matching this schema:");
                    sb.AppendLine("{ \"suggestedHeadline\":string, \"suggestedSummary\":string, \"experienceSuggestions\":string[], \"educationSuggestions\":string[], \"projectSuggestions\":string[], \"skillsSuggestion\":string, \"languagesSuggestion\":string, \"actionableTips\":string[] }");
                    sb.AppendLine();
                    sb.AppendLine("Profile (provide concise, relevant data):");
                    sb.AppendLine($"Name: {student?.FullName}");
                    sb.AppendLine("Educations:");
                    foreach (var ed in profile.Educations)
                        sb.AppendLine($"- {ed.Degree ?? ed.FieldOfStudy} at {ed.Institution} ({ed.StartDate} - {ed.EndDate})");
                    sb.AppendLine("Experiences:");
                    foreach (var ex in profile.Experiences)
                        sb.AppendLine($"- {ex.JobTitle} at {ex.CompanyName}: {ex.Description}");
                    sb.AppendLine("Projects:");
                    foreach (var p in profile.Projects)
                        sb.AppendLine($"- {p.ProjectName}: {p.Description}");
                    sb.AppendLine("Skills:");
                    sb.AppendLine(string.Join(", ", profile.Skills.Select(s => s.SkillName)));
                    sb.AppendLine("Languages:");
                    sb.AppendLine(string.Join(", ", profile.Languages.Select(l => l.LanguageName)));

                    var requestObj = new
                    {
                        model = model,
                        messages = new[]
                        {
                            new { role = "system", content = "You are a helpful assistant that must return valid JSON only." },
                            new { role = "user", content = sb.ToString() }
                        },
                        max_tokens = 800,
                        temperature = 0.6
                    };

                    var reqJson = System.Text.Json.JsonSerializer.Serialize(requestObj);
                    var resp = await client.PostAsync("https://api.openai.com/v1/chat/completions", new StringContent(reqJson, System.Text.Encoding.UTF8, "application/json"));
                    resp.EnsureSuccessStatusCode();
                    var respText = await resp.Content.ReadAsStringAsync();

                    // Parse JSON response safely
                    using var doc = JsonDocument.Parse(respText);
                    if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var content = choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
                        // Attempt to extract the first JSON object from the content
                        var first = content.IndexOf('{');
                        var last = content.LastIndexOf('}');
                        if (first >= 0 && last > first)
                        {
                            var jsonPart = content[first..(last + 1)];
                            var sug = System.Text.Json.JsonSerializer.Deserialize<ProfileSuggestionResult>(jsonPart, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (sug != null) return sug;
                        }
                        // If parsing failed, try to parse the raw content
                        try
                        {
                            var sug2 = System.Text.Json.JsonSerializer.Deserialize<ProfileSuggestionResult>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (sug2 != null) return sug2;
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    // on transient errors, retry with backoff
                    if (attempt >= maxAttempts)
                    {
                        _ = ex; // swallow for fallback
                        break;
                    }
                    var delayMs = 500 * (int)Math.Pow(2, attempt - 1);
                    await Task.Delay(delayMs);
                    continue;
                }
                // if we reach here without returning, break to fallback
                break;
            }
        }

        // Fallback deterministic generator (previous behavior)
        var result = new ProfileSuggestionResult();

        // Top skills
        var topSkills = profile.Skills?.Select(s => s.SkillName).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList() ?? new List<string>();

        // Headline
        var recentExp = profile.Experiences?.OrderByDescending(e => e.CreatedAt).FirstOrDefault();
        if (recentExp != null && !string.IsNullOrWhiteSpace(recentExp.JobTitle))
        {
            var skillPart = topSkills.Any() ? $" · Skills: {string.Join(", ", topSkills)}" : string.Empty;
            result.SuggestedHeadline = $"{recentExp.JobTitle} at {recentExp.CompanyName}{skillPart}".Trim();
        }
        else if (!string.IsNullOrWhiteSpace(student?.FullName) && topSkills.Any())
        {
            result.SuggestedHeadline = $"{student.FullName.Split(' ')[0]} — {string.Join(", ", topSkills.Take(3))}";
        }
        else
        {
            result.SuggestedHeadline = student?.FullName ?? "Student";
        }

        // Summary
        var summarySkills = topSkills.Any() ? string.Join(", ", topSkills) : "relevant skills";
        var summaryRole = recentExp?.JobTitle ?? "aspiring professional";
        result.SuggestedSummary = $"Results-driven {summaryRole} with experience in {summarySkills}. Strong track record delivering quality work and quickly learning new tools. Open to opportunities where I can apply my skills to solve real problems.";

        // Experience suggestions
        if (profile.Experiences != null)
        {
            foreach (var e in profile.Experiences)
            {
                var title = string.IsNullOrWhiteSpace(e.JobTitle) ? "Role" : e.JobTitle;
                var company = string.IsNullOrWhiteSpace(e.CompanyName) ? "Company" : e.CompanyName;
                var desc = string.IsNullOrWhiteSpace(e.Description) ? "Describe your main responsibilities and achievements in short bullet points (use metrics)." : e.Description;
                var shortDesc = desc.Length > 140 ? desc.Substring(0, 137) + "..." : desc;
                result.ExperienceSuggestions.Add($"{title} at {company}: {shortDesc}");
            }
        }

        // Education suggestions
        if (profile.Educations != null)
        {
            foreach (var ed in profile.Educations)
            {
                var inst = ed.Institution ?? "Institution";
                var degree = ed.Degree ?? ed.FieldOfStudy ?? "Degree";
                result.EducationSuggestions.Add($"{degree} — {inst} ({ed.StartDate ?? ""} - {ed.EndDate ?? ""})");
            }
        }

        // Projects
        if (profile.Projects != null)
        {
            foreach (var p in profile.Projects)
            {
                var name = p.ProjectName ?? p.Url ?? "Project";
                var desc = string.IsNullOrWhiteSpace(p.Description) ? "Add a one-line summary of the project and your role/contribution." : p.Description;
                var shortDesc = desc.Length > 140 ? desc.Substring(0, 137) + "..." : desc;
                result.ProjectSuggestions.Add($"{name}: {shortDesc}");
            }
        }

        // Skills suggestion
        if (topSkills.Any())
        {
            result.SkillsSuggestion = $"Feature these top skills near the top of your profile: {string.Join(", ", topSkills)}.";
        }
        else
        {
            result.SkillsSuggestion = "Add your core technical and soft skills (e.g., C#, SQL, problem solving, teamwork).";
        }

        // Languages
        if (profile.Languages != null && profile.Languages.Any())
        {
            result.LanguagesSuggestion = $"List languages with proficiency levels, e.g.: {string.Join(", ", profile.Languages.Select(l => l.LanguageName + " (Intermediate)"))}.";
        }
        else
        {
            result.LanguagesSuggestion = "Add languages you speak and proficiency levels (e.g., English — Native).";
        }

        // Actionable tips
        result.ActionableTips.Add("Use short bullet points for experiences focusing on achievements (use numbers where possible).");
        result.ActionableTips.Add("Keep the summary to 2-4 sentences highlighting your strengths and career goal.");
        result.ActionableTips.Add("Order skills by relevance and recent use.");

        return result;
    }

    public async Task UpdateProfileAsync(int studentId, ProfileUpdateDto update)
    {
        // Upsert sections if provided (merge by Id when possible)
        if (update.Educations != null)
        {
            foreach (var ed in update.Educations)
            {
                if (ed.Id.HasValue)
                {
                    var existing = await _context.Set<StudentEducation>().FirstOrDefaultAsync(x => x.Id == ed.Id.Value && x.StudentId == studentId);
                    if (existing != null)
                    {
                        existing.Institution = ed.Institution;
                        existing.Degree = ed.Degree;
                        existing.FieldOfStudy = ed.FieldOfStudy;
                        existing.StartDate = ed.StartDate;
                        existing.EndDate = ed.EndDate;
                        existing.IsCurrent = ed.IsCurrent;
                        existing.Description = ed.Description;
                        existing.UpdatedAt = DateTime.UtcNow;
                        continue;
                    }
                }

                var entity = new StudentEducation
                {
                    StudentId = studentId,
                    Institution = ed.Institution,
                    Degree = ed.Degree,
                    FieldOfStudy = ed.FieldOfStudy,
                    StartDate = ed.StartDate,
                    EndDate = ed.EndDate,
                    IsCurrent = ed.IsCurrent,
                    Description = ed.Description,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Set<StudentEducation>().Add(entity);
            }
        }

        if (update.Skills != null)
        {
            foreach (var sk in update.Skills)
            {
                if (sk.Id.HasValue)
                {
                    var existing = await _context.Set<StudentSkill>().FirstOrDefaultAsync(x => x.Id == sk.Id.Value && x.StudentId == studentId);
                    if (existing != null)
                    {
                        existing.SkillName = sk.SkillName;
                        existing.ProficiencyLevel = sk.ProficiencyLevel;
                        existing.Category = sk.Category;
                        existing.UpdatedAt = DateTime.UtcNow;
                        continue;
                    }
                }

                var ent = new StudentSkill
                {
                    StudentId = studentId,
                    SkillName = sk.SkillName,
                    ProficiencyLevel = sk.ProficiencyLevel,
                    Category = sk.Category,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Set<StudentSkill>().Add(ent);
            }
        }

        if (update.Experiences != null)
        {
            foreach (var ex in update.Experiences)
            {
                if (ex.Id.HasValue)
                {
                    var existing = await _context.Set<StudentExperience>().FirstOrDefaultAsync(x => x.Id == ex.Id.Value && x.StudentId == studentId);
                    if (existing != null)
                    {
                        existing.CompanyName = ex.CompanyName;
                        existing.JobTitle = ex.JobTitle;
                        existing.StartDate = ex.StartDate;
                        existing.EndDate = ex.EndDate;
                        existing.IsCurrent = ex.IsCurrent;
                        existing.Description = ex.Description;
                        existing.Location = ex.Location;
                        existing.UpdatedAt = DateTime.UtcNow;
                        continue;
                    }
                }

                var ent = new StudentExperience
                {
                    StudentId = studentId,
                    CompanyName = ex.CompanyName,
                    JobTitle = ex.JobTitle,
                    StartDate = ex.StartDate,
                    EndDate = ex.EndDate,
                    IsCurrent = ex.IsCurrent,
                    Description = ex.Description,
                    Location = ex.Location,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Set<StudentExperience>().Add(ent);
            }
        }

        if (update.Projects != null)
        {
            foreach (var pj in update.Projects)
            {
                if (pj.Id.HasValue)
                {
                    var existing = await _context.Set<StudentProject>().FirstOrDefaultAsync(x => x.Id == pj.Id.Value && x.StudentId == studentId);
                    if (existing != null)
                    {
                        existing.ProjectName = pj.ProjectName;
                        existing.StartDate = pj.StartDate;
                        existing.EndDate = pj.EndDate;
                        existing.Description = pj.Description;
                        existing.Technologies = pj.Technologies;
                        existing.Url = pj.Url;
                        existing.UpdatedAt = DateTime.UtcNow;
                        continue;
                    }
                }

                var ent = new StudentProject
                {
                    StudentId = studentId,
                    ProjectName = pj.ProjectName,
                    StartDate = pj.StartDate,
                    EndDate = pj.EndDate,
                    Description = pj.Description,
                    Technologies = pj.Technologies,
                    Url = pj.Url,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Set<StudentProject>().Add(ent);
            }
        }

        if (update.Languages != null)
        {
            foreach (var lg in update.Languages)
            {
                if (lg.Id.HasValue)
                {
                    var existing = await _context.Set<StudentLanguage>().FirstOrDefaultAsync(x => x.Id == lg.Id.Value && x.StudentId == studentId);
                    if (existing != null)
                    {
                        existing.LanguageName = lg.LanguageName;
                        existing.ProficiencyLevel = lg.ProficiencyLevel;
                        existing.UpdatedAt = DateTime.UtcNow;
                        continue;
                    }
                }

                var ent = new StudentLanguage
                {
                    StudentId = studentId,
                    LanguageName = lg.LanguageName,
                    ProficiencyLevel = lg.ProficiencyLevel,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Set<StudentLanguage>().Add(ent);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<ProfileExtractionResult> ExtractAndSaveResumeAsync(int studentId, Stream stream, string contentType, string fileName)
    {
        // Extract data from file (PDF, DOCX, or fallback)
        var extractionResult = await _resumeExtractionService.ExtractFromFileAsync(stream, contentType, fileName);

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

