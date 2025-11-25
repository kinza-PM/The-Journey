using Microsoft.AspNetCore.Mvc;
using TheJourney.Api.Modules.Mobile.Assessment.Controllers;
using TheJourney.Api.Modules.Mobile.Profile.Services;

namespace TheJourney.Api.Modules.Mobile.Profile.Controllers;

[Route("api/mobile/profile")]
public class ProfileSuggestionController : StudentAuthorizedController
{
    private readonly IProfileService _profileService;
    private readonly ILogger<ProfileSuggestionController> _logger;

    public ProfileSuggestionController(IProfileService profileService, ILogger<ProfileSuggestionController> logger)
    {
        _profileService = profileService;
        _logger = logger;
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions()
    {
        var studentId = GetStudentId();
        if (studentId == null)
            return Unauthorized(new { message = "Student ID not found in token." });

        try
        {
            var suggestions = await _profileService.GetAiSuggestionsAsync(studentId.Value);
            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate suggestions for student {StudentId}", studentId);
            return StatusCode(500, new { message = "Failed to generate suggestions." });
        }
    }

    [HttpPatch("")]
    public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateDto update)
    {
        var studentId = GetStudentId();
        if (studentId == null)
            return Unauthorized(new { message = "Student ID not found in token." });

        try
        {
            await _profileService.UpdateProfileAsync(studentId.Value, update);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update profile for student {StudentId}", studentId);
            return StatusCode(500, new { message = "Failed to update profile." });
        }
    }
}
