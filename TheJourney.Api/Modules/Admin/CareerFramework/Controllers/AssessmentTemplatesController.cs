using Microsoft.AspNetCore.Mvc;
using TheJourney.Api.Modules.Admin.CareerFramework.Dtos;
using TheJourney.Api.Modules.Admin.CareerFramework.Services;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Controllers;

[Route("api/admin")]
public class AssessmentTemplatesController : AdminAuthorizedController
{
    private readonly ICareerFrameworkService _careerFrameworkService;

    public AssessmentTemplatesController(ICareerFrameworkService careerFrameworkService)
    {
        _careerFrameworkService = careerFrameworkService;
    }

    [HttpGet("job-roles/{jobRoleId:int}/templates")]
    public async Task<IActionResult> GetTemplatesForJobRoleAsync(int jobRoleId, CancellationToken cancellationToken = default)
    {
        var templates = await _careerFrameworkService.GetTemplatesByJobRoleAsync(jobRoleId, cancellationToken);
        return Ok(templates);
    }

    [HttpGet("templates/{id:int}", Name = "GetAssessmentTemplateById")]
    public async Task<IActionResult> GetTemplateByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var template = await _careerFrameworkService.GetTemplateByIdAsync(id, cancellationToken);
        return template == null ? NotFound() : Ok(template);
    }

    [HttpPost("job-roles/{jobRoleId:int}/templates")]
    public async Task<IActionResult> CreateTemplateAsync(int jobRoleId, [FromBody] AssessmentTemplateRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await _careerFrameworkService.CreateTemplateAsync(jobRoleId, request, GetAdminId(), cancellationToken);
            return CreatedAtRoute("GetAssessmentTemplateById", new { id = template.Id }, template);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

