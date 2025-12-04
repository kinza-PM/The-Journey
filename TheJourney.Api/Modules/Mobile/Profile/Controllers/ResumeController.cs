using Microsoft.AspNetCore.Mvc;
using System.IO;
using TheJourney.Api.Common;
using TheJourney.Api.Modules.Mobile.Assessment.Controllers;
using TheJourney.Api.Modules.Mobile.Profile.Services;

namespace TheJourney.Api.Modules.Mobile.Profile.Controllers;

[Route("api/mobile/resume")]
public class ResumeController : StudentAuthorizedController
{
    private readonly IProfileService _profileService;
    private readonly ILogger<ResumeController> _logger;

    public ResumeController(IProfileService profileService, ILogger<ResumeController> logger)
    {
        _profileService = profileService;
        _logger = logger;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfileAsync()
    {
        var studentId = GetStudentId();
        if (studentId == null)
        {
            return Ok(ApiResponse<object>.Unauthorized("Student ID not found in token."));
        }

        try
        {
            var profile = await _profileService.GetProfileAsync(studentId.Value);
            var response = ApiResponse<object>.Success(new
            {
                studentInfo = profile.StudentInfo,
                educations = profile.Educations,
                skills = profile.Skills,
                experiences = profile.Experiences,
                projects = profile.Projects,
                languages = profile.Languages
            });
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching profile for student {StudentId}", studentId);
            return Ok(ApiResponse<object>.InternalError("An error occurred while fetching the profile. Please try again."));
        }
    }

    [HttpPost("extract")]
    public async Task<IActionResult> ExtractResumeAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var studentId = GetStudentId();
        if (studentId == null)
        {
            return Ok(ApiResponse<object>.Unauthorized("Student ID not found in token."));
        }

        if (file == null || file.Length == 0)
        {
            return Ok(ApiResponse<object>.BadRequest("No file uploaded."));
        }

        // Validate file extension (allow .pdf, .doc, .docx)
        var allowedExt = new[] { ".pdf", ".doc", ".docx" };
        var ext = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        if (!allowedExt.Contains(ext))
        {
            return Ok(ApiResponse<object>.BadRequest("Only .pdf, .doc, and .docx files are allowed."));
        }

        // Validate ContentType if provided (common MIME types)
        var allowedMime = new[] {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
        if (!string.IsNullOrWhiteSpace(file.ContentType) && !allowedMime.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return Ok(ApiResponse<object>.BadRequest($"Invalid file content type: {file.ContentType}. Expected PDF or Word document."));
        }

        // Validate file size (max 20MB)
        const long maxFileSize = 20 * 1024 * 1024; // 20MB
        if (file.Length > maxFileSize)
        {
            return Ok(ApiResponse<object>.BadRequest("File size exceeds the maximum limit of 20MB."));
        }

        

        try
        {
            using var stream = file.OpenReadStream();
            var result = await _profileService.ExtractAndSaveResumeAsync(studentId.Value, stream, file.ContentType ?? string.Empty, file.FileName ?? string.Empty);

            var response = ApiResponse<object>.Success(new
            {
                message = result.Message,
                extracted = new
                {
                    educations = result.EducationsCount,
                    skills = result.SkillsCount,
                    experiences = result.ExperiencesCount,
                    projects = result.ProjectsCount,
                    languages = result.LanguagesCount
                }
            }, "Resume extracted successfully");
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting resume for student {StudentId}", studentId);
            return Ok(ApiResponse<object>.InternalError("An error occurred while processing the resume. Please try again."));
        }
    }

    
}

