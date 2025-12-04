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

        result.PersonalInfo = ExtractPersonalInfo(text);
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
    
    private PersonalInfoData ExtractPersonalInfo(string text)
    {
        var personalInfo = new PersonalInfoData();
        
        // Personal info is usually at the top of the CV (first 500 characters)
        var topSection = text.Length > 500 ? text.Substring(0, 500) : text;
        
        // Extract Name (usually the first line or first capitalized text)
        var namePattern = @"^([A-Z][a-z]+(?:\s+[A-Z][a-z]+){1,3})";
        var nameMatch = Regex.Match(topSection, namePattern, RegexOptions.Multiline);
        if (nameMatch.Success)
        {
            personalInfo.FullName = nameMatch.Groups[1].Value.Trim();
        }
        
        // Extract Email
        var emailPattern = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";
        var emailMatch = Regex.Match(text, emailPattern);
        if (emailMatch.Success)
        {
            personalInfo.Email = emailMatch.Value.Trim();
        }
        
        // Extract Phone (various formats)
        var phonePatterns = new[]
        {
            @"\+?\d{1,4}?[-.\s]?\(?\d{1,4}\)?[-.\s]?\d{1,4}[-.\s]?\d{1,9}", // International or formatted
            @"\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}", // US format
            @"\d{10,15}" // Simple 10-15 digit number
        };
        
        foreach (var pattern in phonePatterns)
        {
            var phoneMatch = Regex.Match(topSection, pattern);
            if (phoneMatch.Success && phoneMatch.Value.Replace(" ", "").Replace("-", "").Replace(".", "").Replace("(", "").Replace(")", "").Length >= 10)
            {
                personalInfo.Phone = phoneMatch.Value.Trim();
                break;
            }
        }
        
        // Extract Address (look for city, state, country patterns)
        var addressPatterns = new[]
        {
            @"(?i)(?:address|location|residence)[\s:]+([^\n]+)", // After "Address:" or "Location:"
            @"([A-Z][a-z]+,\s*[A-Z]{2,}\s*\d{5})", // City, State ZIP
            @"([A-Z][a-z]+,\s*[A-Z][a-z]+(?:,\s*[A-Z][a-z]+)?)", // City, State, Country
            @"(\d+\s+[A-Za-z\s]+,\s*[A-Z][a-z]+,\s*[A-Z]{2,})" // Street, City, State
        };
        
        foreach (var pattern in addressPatterns)
        {
            var addressMatch = Regex.Match(topSection, pattern);
            if (addressMatch.Success)
            {
                personalInfo.Address = addressMatch.Groups[1].Value.Trim();
                if (personalInfo.Address.Length > 10) // Ensure it's substantial
                {
                    break;
                }
            }
        }
        
        // Extract Summary/Professional Summary/Objective (usually after contact info, before experience/education)
        var summaryPatterns = new[]
        {
            @"(?i)(?:professional\s+)?(?:summary|profile|objective|about\s+me|introduction|career\s+summary|career\s+objective)[\s:]*\n\s*([^\n]+(?:\n(?!\s*(?:experience|education|skills|projects|certifications?|languages?|work|employment)\b)[^\n]+)*)",
            @"(?i)(?:professional\s+)?(?:summary|profile|objective|about)[\s:]+([^•\n]{50,500}?)(?=\n\s*(?:experience|education|skills|projects|certifications?|languages?|work|employment|$))"
        };
        
        foreach (var pattern in summaryPatterns)
        {
            var summaryMatch = Regex.Match(text, pattern, RegexOptions.Multiline);
            if (summaryMatch.Success)
            {
                var summary = summaryMatch.Groups[1].Value.Trim();
                
                // Clean up the summary
                summary = Regex.Replace(summary, @"\s+", " "); // Normalize whitespace
                summary = summary.Trim();
                
                // Ensure it's substantial (at least 20 chars and not too long)
                if (summary.Length >= 20 && summary.Length <= 2000)
                {
                    personalInfo.Summary = summary;
                    break;
                }
            }
        }
        
        return personalInfo;
    }

    private List<EducationData> ExtractEducations(string text)
    {
        var educations = new List<EducationData>();
        
        // Look for education section with more variations
        var educationPatterns = new[]
        {
            @"(?i)(?:education|academic\s+(?:background|qualifications?)|qualifications?|degrees?)[\s:]*[\s\S]*?(?=(?:experience|work\s+experience|professional|employment|skills?|technical|projects?|certifications?|languages?|volunteer|references?|$))",
            @"(?i)(?:bachelor|master|phd|doctorate|diploma|certificate|b\.?s\.?|m\.?s\.?|b\.?a\.?|m\.?a\.?)[\s\S]*?(?=(?:experience|work|employment|skills|projects|language|references|$))"
        };
        
        string? educationSection = null;
        foreach (var pattern in educationPatterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success && match.Value.Length > 20) // Ensure we have substantial content
            {
                educationSection = match.Value;
                break;
            }
        }
        
        if (string.IsNullOrWhiteSpace(educationSection))
        {
            return educations;
        }
        
        // Parse line by line to extract multiple education entries
        var lines = educationSection.Split('\n');
        EducationData? currentEducation = null;
        var linesBuffer = new List<string>(); // Buffer to collect related lines
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) 
            {
                // Empty line likely indicates end of current entry
                if (linesBuffer.Count > 0 && currentEducation != null)
                {
                    ProcessEducationBuffer(linesBuffer, currentEducation);
                    
                    // Save if we have meaningful data
                    if (!string.IsNullOrWhiteSpace(currentEducation.Degree) || 
                        !string.IsNullOrWhiteSpace(currentEducation.Institution))
                    {
                        educations.Add(currentEducation);
                        currentEducation = null; // Reset for next entry
                        linesBuffer.Clear();
                    }
                }
                continue;
            }
            
            // Skip section header line
            if (Regex.IsMatch(line, @"(?i)^\s*(?:education|academic|qualification|degrees?)\s*:?\s*$"))
                continue;
            
            // Check patterns that indicate a NEW education entry
            var hasDegreeKeyword = Regex.IsMatch(line, @"(?i)\b(bachelor|master|phd|doctorate|diploma|certificate|b\.?s\.?c?\.?|m\.?s\.?c?\.?|b\.?a\.?|m\.?a\.?|degree)\b");
            var hasUniversityKeyword = Regex.IsMatch(line, @"(?i)\b(university|college|institute|school|academy)\b");
            var startsWithCapital = line.Length > 0 && char.IsUpper(line[0]) && line.Length > 10;
            var hasYearPattern = Regex.IsMatch(line, @"\b\d{4}\b");
            var hasYearRange = Regex.IsMatch(line, @"\d{4}\s*[-–—]\s*\d{4}|\d{4}\s*[-–—to]\s*(?:present|current)|\d{4}\s+to\s+\d{4}", RegexOptions.IgnoreCase);
            var hasBulletOrNumber = Regex.IsMatch(line, @"^[\d•\-\*]+[\.\)\s]");
            
            // Detect if this is a NEW entry (vs continuation of current entry)
            bool isNewEntry = false;
            
            // Strategy: Be aggressive about detecting new entries
            // Most CVs either use bullet points, empty lines, or clear patterns to separate entries
            
            // 1. Has degree keyword - ALWAYS starts new entry
            if (hasDegreeKeyword && currentEducation != null && linesBuffer.Count > 0)
            {
                // We have existing data and found a degree keyword
                isNewEntry = true;
            }
            else if (hasDegreeKeyword && currentEducation == null)
            {
                // First entry with degree
                isNewEntry = true;
            }
            // 2. First entry - any reasonable start
            else if (currentEducation == null && (hasUniversityKeyword || startsWithCapital || hasBulletOrNumber))
            {
                isNewEntry = true;
            }
            // 3. University keyword AND we have collected data (likely 2nd entry starting with university name)
            else if (hasUniversityKeyword && currentEducation != null && linesBuffer.Count >= 2)
            {
                // Already have 2+ lines buffered, new university = new entry
                ProcessEducationBuffer(linesBuffer, currentEducation);
                if (!string.IsNullOrWhiteSpace(currentEducation.Degree) || 
                    !string.IsNullOrWhiteSpace(currentEducation.Institution))
                {
                    isNewEntry = true;
                }
            }
            // 4. Has university AND previous education has both degree and dates (complete entry)
            else if (hasUniversityKeyword && currentEducation != null)
            {
                ProcessEducationBuffer(linesBuffer, currentEducation);
                if ((!string.IsNullOrWhiteSpace(currentEducation.Degree) || 
                     !string.IsNullOrWhiteSpace(currentEducation.Institution)) &&
                    !string.IsNullOrWhiteSpace(currentEducation.StartDate))
                {
                    // Previous entry is complete
                    isNewEntry = true;
                }
            }
            // 5. Year range pattern AND we already have dates (new entry with dates)
            else if (hasYearRange && currentEducation != null)
            {
                ProcessEducationBuffer(linesBuffer, currentEducation);
                if (!string.IsNullOrWhiteSpace(currentEducation.StartDate))
                {
                    // Already have dates, this is a new entry
                    isNewEntry = true;
                }
            }
            // 6. Bullet point or numbered list item
            else if (hasBulletOrNumber && currentEducation != null && linesBuffer.Count > 0)
            {
                isNewEntry = true;
            }
            
            if (isNewEntry)
            {
                // Save previous education if exists
                if (currentEducation != null && linesBuffer.Count > 0)
                {
                    // Make sure it's processed
                    if (string.IsNullOrWhiteSpace(currentEducation.Degree) && 
                        string.IsNullOrWhiteSpace(currentEducation.Institution))
                    {
                        ProcessEducationBuffer(linesBuffer, currentEducation);
                    }
                    
                    // Save if we have meaningful data
                    if (!string.IsNullOrWhiteSpace(currentEducation.Degree) || 
                        !string.IsNullOrWhiteSpace(currentEducation.Institution))
                    {
                        educations.Add(currentEducation);
                    }
                }
                
                // Start new education entry
                currentEducation = new EducationData();
                linesBuffer = new List<string> { line };
            }
            else if (currentEducation != null)
            {
                // Add to current entry's buffer
                linesBuffer.Add(line);
            }
            else
            {
                // No current education yet, start one
                currentEducation = new EducationData();
                linesBuffer = new List<string> { line };
            }
        }
        
        // Don't forget the last education entry
        if (currentEducation != null && linesBuffer.Count > 0)
        {
            ProcessEducationBuffer(linesBuffer, currentEducation);
            
            if (!string.IsNullOrWhiteSpace(currentEducation.Degree) || 
                !string.IsNullOrWhiteSpace(currentEducation.Institution))
            {
                educations.Add(currentEducation);
            }
        }
        
        // If no structured matches, try simpler extraction
        if (educations.Count == 0)
        {
            educations.AddRange(ExtractEducationsSimple(educationSection));
        }
        
        return educations;
    }
    
    private void ProcessEducationBuffer(List<string> lines, EducationData education)
    {
        // Combine all lines to extract information
        var combinedText = string.Join(" ", lines);
        
        // Extract degree from all lines
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(education.Degree))
            {
                education.Degree = ExtractDegree(line);
            }
            
            if (string.IsNullOrWhiteSpace(education.Institution))
            {
                education.Institution = ExtractInstitution(line, education.Degree);
            }
        }
        
        // Extract field of study from combined text
        if (string.IsNullOrWhiteSpace(education.FieldOfStudy))
        {
            var fieldMatch = Regex.Match(combinedText, @"(?i)(?:in|of|major)\s+([A-Z][^,•\n\d]{3,50})|\(([^)]+)\)|,\s*([A-Z][A-Za-z\s&]{3,50}?)\s*(?:,|\||$)");
            if (fieldMatch.Success)
            {
                education.FieldOfStudy = fieldMatch.Groups[1].Success ? fieldMatch.Groups[1].Value.Trim() : 
                                        fieldMatch.Groups[2].Success ? fieldMatch.Groups[2].Value.Trim() : 
                                        fieldMatch.Groups[3].Value.Trim();
                
                // Clean up field of study
                education.FieldOfStudy = Regex.Replace(education.FieldOfStudy, @"(?i)(university|college|institute|from|to|present|current).*$", "").Trim();
            }
            else
            {
                // Try to find field in separate lines (often second line after degree or institution)
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(education.FieldOfStudy) && 
                        !Regex.IsMatch(line, @"\d{4}") && // No dates
                        !Regex.IsMatch(line, @"(?i)(bachelor|master|phd|university|college|institute)") && // Not degree or institution
                        line.Length > 5 && line.Length < 60)
                    {
                        var fieldMatch2 = Regex.Match(line, @"^([A-Z][A-Za-z\s&,]+)");
                        if (fieldMatch2.Success)
                        {
                            education.FieldOfStudy = fieldMatch2.Value.Trim();
                        }
                    }
                }
            }
        }
        
        // Extract dates from all lines
        if (string.IsNullOrWhiteSpace(education.StartDate))
        {
            var datePattern = @"(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+\d{4}|\d{1,2}[/-]\d{4}|\d{4}";
            
            foreach (var line in lines)
            {
                var dateMatches = Regex.Matches(line, datePattern, RegexOptions.IgnoreCase);
                
                if (dateMatches.Count > 0)
                {
                    education.StartDate = dateMatches[0].Value;
                    
                    if (Regex.IsMatch(line, @"(?i)\b(present|current|ongoing|now)\b"))
                    {
                        education.IsCurrent = true;
                    }
                    else if (dateMatches.Count > 1)
                    {
                        education.EndDate = dateMatches[1].Value;
                    }
                    break; // Found dates, no need to continue
                }
            }
        }
        
        // Build the formatted description
        BuildEducationDescription(education);
    }
    
    private void BuildEducationDescription(EducationData education)
    {
        var descriptionParts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(education.Degree))
            descriptionParts.Add(education.Degree);
        
        if (!string.IsNullOrWhiteSpace(education.FieldOfStudy))
            descriptionParts.Add(education.FieldOfStudy);
        
        if (!string.IsNullOrWhiteSpace(education.Institution))
            descriptionParts.Add(education.Institution);
        
        if (!string.IsNullOrWhiteSpace(education.StartDate))
        {
            var dateRange = education.StartDate;
            if (education.IsCurrent)
                dateRange += " - Present";
            else if (!string.IsNullOrWhiteSpace(education.EndDate))
                dateRange += " - " + education.EndDate;
            
            descriptionParts.Add(dateRange);
        }
        
        education.Description = string.Join(" | ", descriptionParts);
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
            
            // Skip section headers
            if (Regex.IsMatch(line, @"(?i)^\s*(?:education|academic|qualification)\s*:?\s*$"))
                continue;
            
            // Check if line contains degree keywords or university keywords
            if (Regex.IsMatch(line, @"(?i)(bachelor|master|phd|doctorate|diploma|certificate|degree|university|college|institute)", RegexOptions.IgnoreCase))
            {
                var education = new EducationData();
                
                education.Degree = ExtractDegree(line);
                education.Institution = ExtractInstitution(line, education.Degree);
                
                // Extract field of study
                var fieldMatch = Regex.Match(line, @"(?i)(?:in|of|major)\s+([A-Z][^,•\n\d]{3,50})|\(([^)]+)\)");
                if (fieldMatch.Success)
                {
                    education.FieldOfStudy = fieldMatch.Groups[1].Success ? fieldMatch.Groups[1].Value.Trim() : fieldMatch.Groups[2].Value.Trim();
                }
                
                // Extract dates
                var dates = Regex.Matches(line, @"(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+\d{4}|\d{1,2}[/-]\d{4}|\d{4}", RegexOptions.IgnoreCase);
                if (dates.Count > 0)
                {
                    education.StartDate = dates[0].Value;
                    
                    if (Regex.IsMatch(line, @"(?i)\b(present|current|ongoing)\b"))
                    {
                        education.IsCurrent = true;
                    }
                    else if (dates.Count > 1)
                    {
                        education.EndDate = dates[1].Value;
                    }
                }
                
                // Build description
                if (!string.IsNullOrWhiteSpace(education.Degree) || !string.IsNullOrWhiteSpace(education.Institution))
                {
                    BuildEducationDescription(education);
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
        
        // Look for experience section with more variations
        var experiencePattern = @"(?i)(?:work\s+)?(?:experience|employment|professional\s+experience|career\s+history|work\s+history|employment\s+history)[\s\S]*?(?=(?:education|academic|skills|technical|projects|certifications?|language|volunteer|references|$))";
        var match = Regex.Match(text, experiencePattern);
        
        string? experienceSection = match.Success ? match.Value : null;
        
        if (string.IsNullOrWhiteSpace(experienceSection))
        {
            return experiences;
        }
        
        // Split section by likely entry delimiters (bullet points, dates patterns)
        var lines = experienceSection.Split('\n');
        ExperienceData? currentExperience = null;
        var descriptionLines = new List<string>();
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            // Check if this line looks like a new job entry (has dates or job title + company pattern)
            var hasDatePattern = Regex.IsMatch(line, @"\d{4}|(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+\d{4}|\d{1,2}[/-]\d{4}");
            var hasJobPattern = Regex.IsMatch(line, @"(?i)(?:at|@|,|\||–|—)\s*[A-Z]");
            
            if (hasDatePattern && hasJobPattern)
            {
                // Save previous experience if exists
                if (currentExperience != null)
                {
                    if (descriptionLines.Count > 0)
                    {
                        currentExperience.Description = string.Join(" ", descriptionLines).Trim();
                    }
                    
                    // Build formatted description
                    BuildExperienceDescription(currentExperience);
                    
                    if (!string.IsNullOrWhiteSpace(currentExperience.JobTitle) || 
                        !string.IsNullOrWhiteSpace(currentExperience.CompanyName))
                    {
                        experiences.Add(currentExperience);
                    }
                }
                
                // Start new experience
                currentExperience = new ExperienceData();
                descriptionLines.Clear();
                
                // Extract job title
                currentExperience.JobTitle = ExtractJobTitle(line);
                
                // Extract company
                currentExperience.CompanyName = ExtractCompanyName(line, currentExperience.JobTitle);
            
            // Extract dates
                ExtractDates(line, currentExperience);
                
                // Extract location
                currentExperience.Location = ExtractLocation(line);
            }
            else if (currentExperience != null)
            {
                // This is likely a description/responsibility line
                var cleanLine = line.TrimStart('•', '-', '*', '·', '◦', '▪').Trim();
                if (!string.IsNullOrWhiteSpace(cleanLine) && cleanLine.Length > 10)
                {
                    descriptionLines.Add(cleanLine);
                }
            }
        }
        
        // Don't forget the last experience
        if (currentExperience != null)
        {
            if (descriptionLines.Count > 0)
            {
                currentExperience.Description = string.Join(" ", descriptionLines).Trim();
            }
            
            // Build formatted description
            BuildExperienceDescription(currentExperience);
            
            if (!string.IsNullOrWhiteSpace(currentExperience.JobTitle) || 
                !string.IsNullOrWhiteSpace(currentExperience.CompanyName))
            {
                experiences.Add(currentExperience);
            }
        }
        
        // If no structured matches, try simpler extraction
        if (experiences.Count == 0)
        {
            experiences.AddRange(ExtractExperiencesSimple(experienceSection));
        }
        
        return experiences;
    }
    
    private void BuildExperienceDescription(ExperienceData experience)
    {
        var descriptionParts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(experience.JobTitle))
            descriptionParts.Add(experience.JobTitle);
        
        if (!string.IsNullOrWhiteSpace(experience.CompanyName))
            descriptionParts.Add(experience.CompanyName);
        
        if (!string.IsNullOrWhiteSpace(experience.Location))
            descriptionParts.Add(experience.Location);
        
        if (!string.IsNullOrWhiteSpace(experience.StartDate))
        {
            var dateRange = experience.StartDate;
            if (experience.IsCurrent)
                dateRange += " - Present";
            else if (!string.IsNullOrWhiteSpace(experience.EndDate))
                dateRange += " - " + experience.EndDate;
            
            descriptionParts.Add(dateRange);
        }
        
        // Prepend the formatted description to any existing description
        var formattedDescription = string.Join(" | ", descriptionParts);
        
        if (!string.IsNullOrWhiteSpace(experience.Description))
        {
            experience.Description = formattedDescription + "\n" + experience.Description;
        }
        else
        {
            experience.Description = formattedDescription;
        }
    }
    
    private void ExtractDates(string text, ExperienceData experience)
    {
        // Enhanced date patterns to handle various formats
        var datePatterns = new[]
        {
            @"(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+\d{4}", // Jan 2020, January 2020
            @"\d{1,2}[/-]\d{4}", // 01/2020, 1/2020
            @"\d{4}" // 2020
        };
        
        var allDates = new List<string>();
        
        foreach (var pattern in datePatterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                allDates.Add(match.Value);
            }
        }
        
        if (allDates.Count > 0)
        {
            experience.StartDate = allDates[0];
            
            // Check for present/current before assigning end date
            if (Regex.IsMatch(text, @"(?i)\b(?:present|current|now|ongoing)\b"))
            {
                experience.IsCurrent = true;
            }
            else if (allDates.Count > 1)
            {
                experience.EndDate = allDates[1];
            }
        }
    }
    
    private string? ExtractLocation(string text)
    {
        // Common location patterns
        var locationPatterns = new[]
        {
            @"(?i)[,|]\s*([A-Z][a-zA-Z\s]+,\s*[A-Z]{2,})\s*(?:[,|•\n]|$)", // City, State/Country
            @"(?i)[,|]\s*([A-Z][a-zA-Z\s]+)\s*,\s*(?:USA|UK|Pakistan|India|Canada)\s*(?:[,|•\n]|$)", // City, Country name
            @"(?i)\|\s*([A-Z][a-zA-Z\s]+(?:,\s*[A-Z]{2,})?)\s*\|" // | Location |
        };
        
        foreach (var pattern in locationPatterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        
        return null;
    }
    
    private string? ExtractJobTitle(string text)
    {
        // Enhanced job title patterns with common seniority levels and roles
        var titlePatterns = new[]
        {
            // Seniority + Role patterns
            @"(?i)(?:^|\s)((?:Chief|C-Level|VP|Vice President|AVP|Assistant Vice President|EVP|Executive Vice President|SVP|Senior Vice President|Head of|Director of|Senior|Lead|Principal|Staff|Junior|Associate|Assistant|Graduate|Intern|Trainee|Entry[-\s]Level)\s+(?:Software|Backend|Frontend|Front-end|Back-end|Full[-\s]?Stack|Mobile|Web|Cloud|DevOps|Data|Machine Learning|AI|Quality Assurance|QA|Test|Product|Project|Program|Engineering|Technical|Business|IT|Systems?|Network|Security|Database|Infrastructure)\s+(?:Engineer|Developer|Programmer|Architect|Manager|Analyst|Consultant|Designer|Specialist|Administrator|Coordinator|Lead|Director|Officer))",
            
            // Direct role patterns
            @"(?i)(?:^|\s)((?:Software|Backend|Frontend|Front-end|Back-end|Full[-\s]?Stack|Mobile|Web|Cloud|DevOps|Data|Machine Learning|AI|Quality Assurance|QA|Test)\s+(?:Engineer|Developer|Programmer|Architect|Manager|Analyst|Designer|Lead))",
            
            // Management patterns
            @"(?i)(?:^|\s)((?:Chief|C-Level|Vice President|VP|Director|Head|Manager|Team Lead|Tech Lead|Technical Lead|Project Manager|Product Manager|Program Manager|Engineering Manager|Operations Manager|IT Manager)\s+(?:of\s+)?(?:\w+)?)",
            
            // Specific roles
            @"(?i)(?:^|\s)((?:Software|Solutions?|Systems?|Enterprise|Cloud|Security|Network|Database|IT)\s+Architect)",
            @"(?i)(?:^|\s)((?:Business|Data|Systems?|Financial|Marketing|Operations?)\s+Analyst)",
            @"(?i)(?:^|\s)((?:UX|UI|Product|Graphic|Web|Mobile)\s+Designer)",
            @"(?i)(?:^|\s)((?:DevOps|Site Reliability|Systems?|Network|Database|Cloud|Security)\s+Engineer)",
            @"(?i)(?:^|\s)(Data\s+(?:Scientist|Engineer|Analyst|Architect))",
            @"(?i)(?:^|\s)((?:Scrum\s+Master|Agile\s+Coach|Product\s+Owner))",
            
            // Consultant/Specialist patterns
            @"(?i)(?:^|\s)((?:Senior|Lead|Principal)?\s*(?:Technical|IT|Business|Management|SAP|Oracle|Salesforce)\s+Consultant)",
            @"(?i)(?:^|\s)((?:\w+)\s+Specialist)",
            
            // Generic patterns with modifiers
            @"(?i)^([A-Z][a-z]+(?:\s+[A-Z][a-z]+){0,3})\s+(?:at|@|\||,|–|—)"
        };
        
        foreach (var pattern in titlePatterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success && match.Groups.Count > 1)
            {
                var title = match.Groups[1].Value.Trim();
                // Clean up common artifacts
                title = Regex.Replace(title, @"\s+at$|\s+@$|\s+of$", "", RegexOptions.IgnoreCase).Trim();
                if (title.Length >= 5 && title.Length <= 100)
                {
                    return title;
                }
            }
        }
        
        // Fallback: Take first substantial capitalized phrase before "at" or "@"
        var beforeAtMatch = Regex.Match(text, @"^([A-Z][^@|\n]{5,80}?)\s+(?:at|@)", RegexOptions.IgnoreCase);
        if (beforeAtMatch.Success)
        {
            return beforeAtMatch.Groups[1].Value.Trim();
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
        
        // Enhanced company extraction patterns
        var companyPatterns = new[]
        {
            // After "at" or "@"
            @"(?i)(?:at|@)\s+([A-Z][A-Za-z0-9\s&.,'()-]{2,80}?)(?:\s*[,|\n•]|\s+(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec|\d{4}|\d{1,2}/))",
            
            // Between pipes or separators
            @"\|\s*([A-Z][A-Za-z0-9\s&.,'()-]{2,60}?)\s*\|",
            
            // After comma (common in some formats)
            @"(?i),\s+([A-Z][A-Za-z0-9\s&.,'()-]{3,60}?)\s*(?:[,|\n•]|\s+(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec|\d{4}))",
            
            // Company with common suffixes
            @"(?i)(?:at|@)\s+([A-Z][A-Za-z0-9\s&.'()-]+?(?:\s+(?:Inc\.?|LLC|Ltd\.?|Limited|Corp\.?|Corporation|Co\.?|Company|Group|Technologies|Technology|Tech|Solutions?|Services?|Systems?|Consulting|International|Global)))",
            
            // Generic after "at"
            @"(?i)(?:at|@)\s+([A-Z][A-Za-z0-9\s&.,'()-]+)"
        };
        
        foreach (var pattern in companyPatterns)
        {
            var match = Regex.Match(textWithoutTitle, pattern);
            if (match.Success && match.Groups.Count > 1)
            {
                var company = match.Groups[1].Value.Trim();
                
                // Clean up the company name
                company = CleanCompanyName(company);
                
                if (company.Length >= 2 && company.Length <= 100)
                {
                    return company;
                }
            }
        }
        
        // Fallback: look for capitalized words after the title
        var words = textWithoutTitle.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var companyWords = new List<string>();
        
        foreach (var word in words)
        {
            if (word.Length > 0 && char.IsUpper(word[0]) && !word.All(char.IsDigit))
            {
                companyWords.Add(word);
                if (companyWords.Count >= 3) break; // Limit to 3 words
            }
        }
        
        if (companyWords.Count > 0)
        {
            var company = string.Join(" ", companyWords);
            return CleanCompanyName(company);
        }
        
        return null;
    }
    
    private string CleanCompanyName(string company)
    {
        // Remove date patterns
        company = Regex.Replace(company, @"\s*(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+\d{4}.*$", "", RegexOptions.IgnoreCase);
        company = Regex.Replace(company, @"\s*\d{1,2}[/-]\d{4}.*$", "");
        company = Regex.Replace(company, @"\s*\d{4}.*$", "");
        
        // Remove trailing punctuation and separators
        company = Regex.Replace(company, @"[,|\-–—•]+$", "");
        
        // Remove "present", "current" etc.
        company = Regex.Replace(company, @"\s*(?:present|current|now|ongoing).*$", "", RegexOptions.IgnoreCase);
        
        // Trim whitespace
        company = company.Trim();
        
        return company;
    }
    
    private List<ExperienceData> ExtractExperiencesSimple(string text)
    {
        var experiences = new List<ExperienceData>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            if (line.Length < 10) continue;
            
            // Skip if it looks like a section header
            if (Regex.IsMatch(line, @"(?i)^\s*(?:experience|work|employment|professional|career)\s*:?\s*$"))
                continue;
            
            var experience = new ExperienceData();
            experience.JobTitle = ExtractJobTitle(line);
            experience.CompanyName = ExtractCompanyName(line, experience.JobTitle);
            experience.Location = ExtractLocation(line);
            
            // Extract dates using the enhanced method
            ExtractDates(line, experience);
            
            // Build description
            if (!string.IsNullOrWhiteSpace(experience.JobTitle) || !string.IsNullOrWhiteSpace(experience.CompanyName))
            {
                BuildExperienceDescription(experience);
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
            var descriptionText = remainingText.Substring(0, Math.Min(descriptionEnd, 500)).Trim();
            
            // Extract technologies
            var techPattern = @"(?i)(?:technologies?|tech|tools?|stack)[:•\s]+([^•\n]+)";
            var techMatch = Regex.Match(descriptionText, techPattern);
            if (techMatch.Success)
            {
                project.Technologies = techMatch.Groups[1].Value.Trim();
            }
            
            // Extract URL
            var urlPattern = @"(https?://[^\s]+|www\.[^\s]+)";
            var urlMatch = Regex.Match(descriptionText, urlPattern);
            if (urlMatch.Success)
            {
                project.Url = urlMatch.Value;
            }
            
            // Extract dates
            var datePattern = @"(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+\d{4}|\d{1,2}[/-]\d{4}|\d{4}";
            var dateMatches = Regex.Matches(descriptionText, datePattern, RegexOptions.IgnoreCase);
            if (dateMatches.Count > 0)
            {
                project.StartDate = dateMatches[0].Value;
                if (dateMatches.Count > 1)
                {
                    project.EndDate = dateMatches[1].Value;
                }
            }
            
            // Store the raw description text (will be formatted below)
            var rawDescription = descriptionText.Length > 20 ? descriptionText : null;
            
            // Build formatted description with header
            BuildProjectDescription(project, rawDescription);
            
            projects.Add(project);
        }
        
        return projects;
    }
    
    private void BuildProjectDescription(ProjectData project, string? rawDescription)
    {
        var descriptionParts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(project.ProjectName))
            descriptionParts.Add(project.ProjectName);
        
        if (!string.IsNullOrWhiteSpace(project.Technologies))
            descriptionParts.Add(project.Technologies);
        
        if (!string.IsNullOrWhiteSpace(project.StartDate))
        {
            var dateRange = project.StartDate;
            if (!string.IsNullOrWhiteSpace(project.EndDate))
                dateRange += " - " + project.EndDate;
            
            descriptionParts.Add(dateRange);
        }
        
        if (!string.IsNullOrWhiteSpace(project.Url))
            descriptionParts.Add(project.Url);
        
        // Build formatted header
        var formattedHeader = string.Join(" | ", descriptionParts);
        
        // Combine header with raw description
        if (!string.IsNullOrWhiteSpace(rawDescription))
        {
            project.Description = formattedHeader + "\n" + rawDescription;
        }
        else
        {
            project.Description = formattedHeader;
        }
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

