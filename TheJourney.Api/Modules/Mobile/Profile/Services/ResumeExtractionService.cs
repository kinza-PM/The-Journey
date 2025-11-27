using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TheJourney.Api.Modules.Mobile.Profile.Models;

namespace TheJourney.Api.Modules.Mobile.Profile.Services;

public class ResumeExtractionService : IResumeExtractionService
{
    public async Task<ResumeExtractionResult> ExtractFromFileAsync(Stream stream, string contentType, string fileName)
    {
        // Normalize parameters
        contentType ??= string.Empty;
        fileName ??= string.Empty;

        // Decide by extension first for robustness
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        string text = string.Empty;

        try
        {
            if (ext == ".pdf" || contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                text = await ExtractTextFromPdfAsync(stream);
            }
            else if (ext == ".docx" || contentType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase))
            {
                text = await ExtractTextFromDocxAsync(stream);
            }
            else if (ext == ".doc" || contentType.Equals("application/msword", StringComparison.OrdinalIgnoreCase))
            {
                // Best-effort fallback for legacy .doc
                text = await ExtractTextFallbackAsync(stream);
            }
            else
            {
                // Try to detect by content-type; fallback to PDF extraction attempt
                if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
                {
                    text = await ExtractTextFromPdfAsync(stream);
                }
                else if (contentType.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase) || contentType.Contains("openxml", StringComparison.OrdinalIgnoreCase))
                {
                    text = await ExtractTextFromDocxAsync(stream);
                }
                else
                {
                    // Last resort: try PDF extraction then text fallback
                    text = await ExtractTextFromPdfAsync(stream);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        text = await ExtractTextFallbackAsync(stream);
                    }
                }
            }
        }
        catch
        {
            // On any extraction error, attempt fallback
            try { text = await ExtractTextFallbackAsync(stream); } catch { text = string.Empty; }
        }

        var result = new ResumeExtractionResult();

        if (string.IsNullOrWhiteSpace(text)) return result;

        text = NormalizeText(text);

        result.Educations = ExtractEducations(text);
        result.Skills = ExtractSkills(text);
        result.Experiences = ExtractExperiences(text);
        result.Projects = ExtractProjects(text);
        result.Languages = ExtractLanguages(text);

        return result;
    }
    
    private async Task<string> ExtractTextFromPdfAsync(Stream pdfStream)
    {
        var textBuilder = new StringBuilder();
        // Ensure stream position at start
        if (pdfStream.CanSeek) pdfStream.Seek(0, SeekOrigin.Begin);

        using var document = PdfDocument.Open(pdfStream);
        foreach (var page in document.GetPages())
        {
            textBuilder.AppendLine(page.Text);
        }

        return textBuilder.ToString();
    }

    private async Task<string> ExtractTextFromDocxAsync(Stream docxStream)
    {
        if (docxStream.CanSeek) docxStream.Seek(0, SeekOrigin.Begin);

        using var mem = new MemoryStream();
        await docxStream.CopyToAsync(mem);
        mem.Seek(0, SeekOrigin.Begin);

        using var word = WordprocessingDocument.Open(mem, false);
        var body = word.MainDocumentPart?.Document?.Body;
        if (body == null) return string.Empty;

        return body.InnerText ?? string.Empty;
    }

    private async Task<string> ExtractTextFallbackAsync(Stream stream)
    {
        // Best-effort plain text extraction for legacy .doc or unknown types
        if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);
        using var mem = new MemoryStream();
        await stream.CopyToAsync(mem);
        var bytes = mem.ToArray();

        // Try UTF8 then fallback to Latin1
        try
        {
            var text = Encoding.UTF8.GetString(bytes);
            if (!string.IsNullOrWhiteSpace(text) && text.Any(c => !char.IsControl(c) || c=='\n' || c=='\r'))
                return text;
        }
        catch { }

        try
        {
            var text = Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
            return text;
        }
        catch { }

        return string.Empty;
    }
    
    private string NormalizeText(string text)
    {
        // Replace multiple whitespaces with single space
        text = Regex.Replace(text, @"\s+", " ");
        // Replace multiple newlines with single newline
        text = Regex.Replace(text, @"\n\s*\n", "\n");
        return text;
    }
    
    private List<EducationData> ExtractEducations(string text)
    {
        var educations = new List<EducationData>();
        
        // Look for education section
        var educationPatterns = new[]
        {
            @"(?i)(?:education|academic|qualification|degree|university|college|school)[\s\S]*?(?=(?:experience|work|employment|skills|projects|language|$))",
            @"(?i)(?:bachelor|master|phd|doctorate|diploma|certificate)[\s\S]*?(?=(?:experience|work|employment|skills|projects|language|$))"
        };
        
        string? educationSection = null;
        foreach (var pattern in educationPatterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success)
            {
                educationSection = match.Value;
                break;
            }
        }
        
        if (string.IsNullOrWhiteSpace(educationSection))
        {
            return educations;
        }
        
        // Extract individual education entries
        // Pattern for degree/institution/date
        var educationEntryPattern = @"(?i)(?:^|\n)\s*([A-Z][^•\n]{10,100}?)\s*(?:[-–—]\s*)?([A-Z][^•\n]{10,100}?)?\s*(?:[-–—]\s*)?(\d{4}|\w+\s+\d{4}|\d{1,2}[/-]\d{4})\s*(?:[-–—]\s*)?(?:present|current|(\d{4}|\w+\s+\d{4}|\d{1,2}[/-]\d{4}))?";
        
        var matches = Regex.Matches(educationSection, educationEntryPattern, RegexOptions.Multiline);
        
        foreach (Match match in matches)
        {
            var education = new EducationData();
            
            // Try to identify degree and institution
            var fullText = match.Groups[0].Value.Trim();
            
            // Look for degree keywords
            var degreeKeywords = new[] { "Bachelor", "Master", "PhD", "Doctorate", "Diploma", "Certificate", "BSc", "MSc", "BA", "MA", "BS", "MS" };
            foreach (var keyword in degreeKeywords)
            {
                if (fullText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    education.Degree = ExtractDegree(fullText);
                    break;
                }
            }
            
            // Extract institution (usually first part or after degree)
            education.Institution = ExtractInstitution(fullText, education.Degree);
            
            // Extract dates
            var datePattern = @"(\d{4}|\w+\s+\d{4}|\d{1,2}[/-]\d{4})";
            var dateMatches = Regex.Matches(fullText, datePattern);
            if (dateMatches.Count > 0)
            {
                education.StartDate = dateMatches[0].Value;
                if (dateMatches.Count > 1)
                {
                    education.EndDate = dateMatches[1].Value;
                }
                else if (fullText.Contains("present", StringComparison.OrdinalIgnoreCase) || 
                         fullText.Contains("current", StringComparison.OrdinalIgnoreCase))
                {
                    education.IsCurrent = true;
                }
            }
            
            // Extract field of study (often after degree or in parentheses)
            var fieldMatch = Regex.Match(fullText, @"(?:in|of)\s+([A-Z][^,•\n]{5,50})|\(([^)]+)\)");
            if (fieldMatch.Success)
            {
                education.FieldOfStudy = fieldMatch.Groups[1].Success ? fieldMatch.Groups[1].Value : fieldMatch.Groups[2].Value;
            }
            
            if (!string.IsNullOrWhiteSpace(education.Degree) || !string.IsNullOrWhiteSpace(education.Institution))
            {
                educations.Add(education);
            }
        }
        
        // If no structured matches, try simpler extraction
        if (educations.Count == 0)
        {
            educations.AddRange(ExtractEducationsSimple(educationSection));
        }
        
        return educations;
    }
    
    private string? ExtractDegree(string text)
    {
        var degreePatterns = new[]
        {
            @"(?:Bachelor|B\.?S\.?|B\.?A\.?|B\.?Sc\.?)\s+(?:of\s+)?(?:Science|Arts|Engineering|Technology|Business|Computer Science|Information Technology)?",
            @"(?:Master|M\.?S\.?|M\.?A\.?|M\.?Sc\.?|M\.?Tech\.?)\s+(?:of\s+)?(?:Science|Arts|Engineering|Technology|Business|Computer Science|Information Technology)?",
            @"(?:PhD|Ph\.?D\.?|Doctorate)\s+(?:in\s+)?([A-Z][^,•\n]{5,50})?",
            @"(?:Diploma|Certificate)\s+(?:in\s+)?([A-Z][^,•\n]{5,50})?"
        };
        
        foreach (var pattern in degreePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Value.Trim();
            }
        }
        
        return null;
    }
    
    private string? ExtractInstitution(string text, string? degree)
    {
        // Remove degree from text
        var textWithoutDegree = text;
        if (!string.IsNullOrWhiteSpace(degree))
        {
            textWithoutDegree = text.Replace(degree, "", StringComparison.OrdinalIgnoreCase);
        }
        
        // Look for common institution indicators
        var institutionPattern = @"([A-Z][A-Za-z\s&]{5,80}?(?:University|College|Institute|School|Academy))";
        var match = Regex.Match(textWithoutDegree, institutionPattern);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        
        // If no institution keyword, take first substantial capitalized phrase
        var words = textWithoutDegree.Split(new[] { ' ', '\t', '\n', '-', '–', '—' }, StringSplitOptions.RemoveEmptyEntries);
        var institutionWords = words.Take(3).Where(w => char.IsUpper(w[0])).ToList();
        if (institutionWords.Count >= 2)
        {
            return string.Join(" ", institutionWords);
        }
        
        return null;
    }
    
    private List<EducationData> ExtractEducationsSimple(string text)
    {
        var educations = new List<EducationData>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            if (line.Length < 10) continue;
            
            var education = new EducationData();
            
            // Check if line contains degree keywords
            if (Regex.IsMatch(line, @"(?i)(bachelor|master|phd|doctorate|diploma|certificate|degree)", RegexOptions.IgnoreCase))
            {
                education.Degree = ExtractDegree(line);
                education.Institution = ExtractInstitution(line, education.Degree);
                
                var dates = Regex.Matches(line, @"\d{4}");
                if (dates.Count > 0)
                {
                    education.StartDate = dates[0].Value;
                    if (dates.Count > 1)
                    {
                        education.EndDate = dates[1].Value;
                    }
                }
                
                if (!string.IsNullOrWhiteSpace(education.Degree) || !string.IsNullOrWhiteSpace(education.Institution))
                {
                    educations.Add(education);
                }
            }
        }
        
        return educations;
    }
    
    private List<string> ExtractSkills(string text)
    {
        var skills = new List<string>();
        
        // Look for skills section
        var skillsPattern = @"(?i)(?:skills|technical\s+skills|competencies|expertise)[\s\S]*?(?=(?:experience|education|projects|language|$))";
        var match = Regex.Match(text, skillsPattern);
        
        string? skillsSection = match.Success ? match.Value : null;
        
        if (string.IsNullOrWhiteSpace(skillsSection))
        {
            // Try to find skills inline
            skillsSection = text;
        }
        
        // Common technical skills patterns
        var skillKeywords = new[]
        {
            // Programming languages
            "C#", "Java", "Python", "JavaScript", "TypeScript", "C++", "C", "PHP", "Ruby", "Go", "Rust", "Swift", "Kotlin",
            // Frameworks
            ".NET", "ASP.NET", "React", "Angular", "Vue", "Node.js", "Express", "Django", "Flask", "Spring", "Laravel",
            // Databases
            "SQL", "MySQL", "PostgreSQL", "MongoDB", "Redis", "Oracle", "SQL Server",
            // Tools & Technologies
            "Git", "Docker", "Kubernetes", "AWS", "Azure", "GCP", "Linux", "Windows", "Agile", "Scrum", "CI/CD", "Jenkins",
            // Other common skills
            "HTML", "CSS", "REST", "API", "GraphQL", "Microservices", "Machine Learning", "AI", "Data Science"
        };
        
        foreach (var keyword in skillKeywords)
        {
            if (skillsSection.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                skills.Add(keyword);
            }
        }
        
        // Extract skills from bullet points or comma-separated lists
        var skillListPattern = @"(?i)(?:skills?|technical\s+skills?)[:•\s]+([^•\n]+)";
        var skillMatches = Regex.Matches(skillsSection ?? text, skillListPattern);
        
        foreach (Match skillMatch in skillMatches)
        {
            var skillText = skillMatch.Groups[1].Value;
            var skillItems = skillText.Split(new[] { ',', ';', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var item in skillItems)
            {
                var skill = item.Trim();
                if (skill.Length > 2 && skill.Length < 50 && !skills.Contains(skill, StringComparer.OrdinalIgnoreCase))
                {
                    skills.Add(skill);
                }
            }
        }
        
        return skills.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
    
    private List<ExperienceData> ExtractExperiences(string text)
    {
        var experiences = new List<ExperienceData>();
        
        // Look for experience section
        var experiencePattern = @"(?i)(?:experience|work\s+experience|employment|professional\s+experience|career)[\s\S]*?(?=(?:education|skills|projects|language|$))";
        var match = Regex.Match(text, experiencePattern);
        
        string? experienceSection = match.Success ? match.Value : null;
        
        if (string.IsNullOrWhiteSpace(experienceSection))
        {
            return experiences;
        }
        
        // Extract job entries - look for job titles and companies
        var jobEntryPattern = @"(?i)(?:^|\n)\s*([A-Z][^•\n]{10,100}?)\s*(?:at|@|-|–|—)\s*([A-Z][^•\n]{10,100}?)?\s*(?:[-–—]\s*)?(\d{4}|\w+\s+\d{4}|\d{1,2}[/-]\d{4})\s*(?:[-–—]\s*)?(?:present|current|(\d{4}|\w+\s+\d{4}|\d{1,2}[/-]\d{4}))?";
        
        var matches = Regex.Matches(experienceSection, jobEntryPattern, RegexOptions.Multiline);
        
        foreach (Match matchItem in matches)
        {
            var experience = new ExperienceData();
            var fullText = matchItem.Groups[0].Value.Trim();
            
            // Extract job title (usually first part)
            experience.JobTitle = ExtractJobTitle(fullText);
            
            // Extract company (usually after "at" or "@" or second part)
            experience.CompanyName = ExtractCompanyName(fullText, experience.JobTitle);
            
            // Extract dates
            var datePattern = @"(\d{4}|\w+\s+\d{4}|\d{1,2}[/-]\d{4})";
            var dateMatches = Regex.Matches(fullText, datePattern);
            if (dateMatches.Count > 0)
            {
                experience.StartDate = dateMatches[0].Value;
                if (dateMatches.Count > 1)
                {
                    experience.EndDate = dateMatches[1].Value;
                }
                else if (fullText.Contains("present", StringComparison.OrdinalIgnoreCase) || 
                         fullText.Contains("current", StringComparison.OrdinalIgnoreCase))
                {
                    experience.IsCurrent = true;
                }
            }
            
            // Extract location if present
            var locationMatch = Regex.Match(fullText, @"(?:in|at|,)\s*([A-Z][A-Za-z\s,]{3,50}?)(?:,|\n|$)");
            if (locationMatch.Success)
            {
                experience.Location = locationMatch.Groups[1].Value.Trim();
            }
            
            if (!string.IsNullOrWhiteSpace(experience.JobTitle) || !string.IsNullOrWhiteSpace(experience.CompanyName))
            {
                experiences.Add(experience);
            }
        }
        
        // If no structured matches, try simpler extraction
        if (experiences.Count == 0)
        {
            experiences.AddRange(ExtractExperiencesSimple(experienceSection));
        }
        
        return experiences;
    }
    
    private string? ExtractJobTitle(string text)
    {
        // Common job title patterns
        var titlePatterns = new[]
        {
            @"(?:^|\s)(Software\s+Engineer|Developer|Programmer|Senior\s+Developer|Lead\s+Developer|Architect|Manager|Analyst|Consultant|Designer|Engineer)[\s,]",
            @"([A-Z][a-z]+\s+(?:Engineer|Developer|Manager|Analyst|Consultant|Designer|Specialist|Architect))"
        };
        
        foreach (var pattern in titlePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        
        // Take first substantial capitalized phrase
        var words = text.Split(new[] { ' ', '\t', '\n', '-', '–', '—', '@' }, StringSplitOptions.RemoveEmptyEntries);
        var titleWords = words.Take(3).Where(w => char.IsUpper(w[0])).ToList();
        if (titleWords.Count >= 2)
        {
            return string.Join(" ", titleWords);
        }
        
        return null;
    }
    
    private string? ExtractCompanyName(string text, string? jobTitle)
    {
        var textWithoutTitle = text;
        if (!string.IsNullOrWhiteSpace(jobTitle))
        {
            textWithoutTitle = text.Replace(jobTitle, "", StringComparison.OrdinalIgnoreCase);
        }
        
        // Look for company after "at" or "@"
        var companyPattern = @"(?:at|@)\s+([A-Z][A-Za-z\s&.,]{3,80}?)(?:\s|,|\n|$)";
        var match = Regex.Match(textWithoutTitle, companyPattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        
        // Take next substantial capitalized phrase
        var parts = textWithoutTitle.Split(new[] { ' ', '\t', '\n', '-', '–', '—' }, StringSplitOptions.RemoveEmptyEntries);
        var companyWords = parts.Skip(3).Take(3).Where(w => char.IsUpper(w[0])).ToList();
        if (companyWords.Count >= 1)
        {
            return string.Join(" ", companyWords);
        }
        
        return null;
    }
    
    private List<ExperienceData> ExtractExperiencesSimple(string text)
    {
        var experiences = new List<ExperienceData>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            if (line.Length < 10) continue;
            
            var experience = new ExperienceData();
            experience.JobTitle = ExtractJobTitle(line);
            experience.CompanyName = ExtractCompanyName(line, experience.JobTitle);
            
            var dates = Regex.Matches(line, @"\d{4}");
            if (dates.Count > 0)
            {
                experience.StartDate = dates[0].Value;
                if (dates.Count > 1)
                {
                    experience.EndDate = dates[1].Value;
                }
            }
            
            if (!string.IsNullOrWhiteSpace(experience.JobTitle) || !string.IsNullOrWhiteSpace(experience.CompanyName))
            {
                experiences.Add(experience);
            }
        }
        
        return experiences;
    }
    
    private List<ProjectData> ExtractProjects(string text)
    {
        var projects = new List<ProjectData>();
        
        // Look for projects section
        var projectsPattern = @"(?i)(?:projects?|portfolio|work\s+projects?)[\s\S]*?(?=(?:experience|education|skills|language|$))";
        var match = Regex.Match(text, projectsPattern);
        
        string? projectsSection = match.Success ? match.Value : null;
        
        if (string.IsNullOrWhiteSpace(projectsSection))
        {
            return projects;
        }
        
        // Extract project entries
        var projectEntryPattern = @"(?i)(?:^|\n|•)\s*([A-Z][^•\n]{10,100}?)(?:\s|:|\n)";
        var matches = Regex.Matches(projectsSection, projectEntryPattern, RegexOptions.Multiline);
        
        foreach (Match matchItem in matches)
        {
            var projectName = matchItem.Groups[1].Value.Trim();
            
            // Skip if it looks like a job title or company
            if (projectName.Contains(" at ", StringComparison.OrdinalIgnoreCase) ||
                projectName.Contains(" @ ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            var project = new ProjectData
            {
                ProjectName = projectName
            };
            
            // Try to extract description from following lines
            var startIndex = matchItem.Index + matchItem.Length;
            var remainingText = projectsSection.Substring(startIndex);
            var nextEntry = Regex.Match(remainingText, @"(?i)(?:^|\n|•)\s*([A-Z][^•\n]{10,100}?)");
            var descriptionEnd = nextEntry.Success ? nextEntry.Index : remainingText.Length;
            var description = remainingText.Substring(0, Math.Min(descriptionEnd, 500)).Trim();
            
            if (description.Length > 20)
            {
                project.Description = description;
            }
            
            // Extract technologies
            var techPattern = @"(?i)(?:technologies?|tech|tools?|stack)[:•\s]+([^•\n]+)";
            var techMatch = Regex.Match(description, techPattern);
            if (techMatch.Success)
            {
                project.Technologies = techMatch.Groups[1].Value.Trim();
            }
            
            // Extract URL
            var urlPattern = @"(https?://[^\s]+|www\.[^\s]+)";
            var urlMatch = Regex.Match(description, urlPattern);
            if (urlMatch.Success)
            {
                project.Url = urlMatch.Value;
            }
            
            projects.Add(project);
        }
        
        return projects;
    }
    
    private List<LanguageData> ExtractLanguages(string text)
    {
        var languages = new List<LanguageData>();
        
        // Look for languages section
        var languagesPattern = @"(?i)(?:languages?|language\s+skills?)[\s\S]*?(?=(?:experience|education|skills|projects|$))";
        var match = Regex.Match(text, languagesPattern);
        
        string? languagesSection = match.Success ? match.Value : null;
        
        if (string.IsNullOrWhiteSpace(languagesSection))
        {
            return languages;
        }
        
        // Common languages
        var commonLanguages = new[]
        {
            "English", "Spanish", "French", "German", "Italian", "Portuguese", "Chinese", "Japanese", "Korean",
            "Arabic", "Hindi", "Russian", "Dutch", "Swedish", "Norwegian", "Danish", "Finnish", "Polish",
            "Turkish", "Greek", "Hebrew", "Thai", "Vietnamese", "Indonesian", "Malay", "Tagalog"
        };
        
        foreach (var lang in commonLanguages)
        {
            if (languagesSection.Contains(lang, StringComparison.OrdinalIgnoreCase))
            {
                var language = new LanguageData { LanguageName = lang };
                
                // Try to extract proficiency level
                var proficiencyPattern = $@"{lang}[\s:]+(?:[-–—]?\s*)?(?:proficient|fluent|native|basic|intermediate|advanced|beginner|expert|native\s+speaker)";
                var profMatch = Regex.Match(languagesSection, proficiencyPattern, RegexOptions.IgnoreCase);
                if (profMatch.Success)
                {
                    var profText = profMatch.Value;
                    if (profText.Contains("native", StringComparison.OrdinalIgnoreCase))
                        language.ProficiencyLevel = "Native";
                    else if (profText.Contains("fluent", StringComparison.OrdinalIgnoreCase))
                        language.ProficiencyLevel = "Fluent";
                    else if (profText.Contains("proficient", StringComparison.OrdinalIgnoreCase))
                        language.ProficiencyLevel = "Proficient";
                    else if (profText.Contains("advanced", StringComparison.OrdinalIgnoreCase))
                        language.ProficiencyLevel = "Advanced";
                    else if (profText.Contains("intermediate", StringComparison.OrdinalIgnoreCase))
                        language.ProficiencyLevel = "Intermediate";
                    else if (profText.Contains("basic", StringComparison.OrdinalIgnoreCase) || profText.Contains("beginner", StringComparison.OrdinalIgnoreCase))
                        language.ProficiencyLevel = "Basic";
                }
                
                languages.Add(language);
            }
        }
        
        // Extract from list format
        var listPattern = @"(?i)(?:languages?)[:•\s]+([^•\n]+)";
        var listMatch = Regex.Match(languagesSection, listPattern);
        if (listMatch.Success)
        {
            var langText = listMatch.Groups[1].Value;
            var langItems = langText.Split(new[] { ',', ';', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var item in langItems)
            {
                var langName = item.Trim();
                if (langName.Length > 2 && langName.Length < 50)
                {
                    var language = new LanguageData { LanguageName = langName };
                    
                    // Check for proficiency in the item
                    if (item.Contains("(", StringComparison.OrdinalIgnoreCase))
                    {
                        var profMatch = Regex.Match(item, @"\(([^)]+)\)");
                        if (profMatch.Success)
                        {
                            language.ProficiencyLevel = profMatch.Groups[1].Value.Trim();
                        }
                    }
                    
                    if (!languages.Any(l => l.LanguageName.Equals(language.LanguageName, StringComparison.OrdinalIgnoreCase)))
                    {
                        languages.Add(language);
                    }
                }
            }
        }
        
        return languages;
    }
}

