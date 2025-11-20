using Microsoft.AspNetCore.Mvc;
using TheJourney.Api.Modules.Mobile.Assessment.Services;

namespace TheJourney.Api.Modules.Mobile.Assessment.Controllers;

[Route("api/mobile/career")]
public class CareerController : StudentAuthorizedController
{
    private readonly IAssessmentService _assessmentService;

    public CareerController(IAssessmentService assessmentService)
    {
        _assessmentService = assessmentService;
    }

    [HttpGet("industries")]
    public async Task<IActionResult> GetIndustriesAsync(CancellationToken cancellationToken = default)
    {
        var studentId = GetStudentId();
        if (studentId == null)
        {
            return Unauthorized();
        }

        var industries = await _assessmentService.GetIndustriesAsync(studentId.Value, cancellationToken);
        return Ok(industries);
    }

    [HttpGet("industries/{industryId:int}/majors")]
    public async Task<IActionResult> GetMajorsAsync(int industryId, CancellationToken cancellationToken = default)
    {
        var majors = await _assessmentService.GetMajorsAsync(industryId, cancellationToken);
        return Ok(majors);
    }

    [HttpGet("majors/{majorId:int}/job-roles")]
    public async Task<IActionResult> GetJobRolesAsync(int majorId, CancellationToken cancellationToken = default)
    {
        var roles = await _assessmentService.GetJobRolesAsync(majorId, cancellationToken);
        return Ok(roles);
    }
}

