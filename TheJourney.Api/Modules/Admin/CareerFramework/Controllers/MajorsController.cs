using Microsoft.AspNetCore.Mvc;
using TheJourney.Api.Modules.Admin.CareerFramework.Dtos;
using TheJourney.Api.Modules.Admin.CareerFramework.Services;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Controllers;

[Route("api/admin")]
public class MajorsController : AdminAuthorizedController
{
    private readonly ICareerFrameworkService _careerFrameworkService;

    public MajorsController(ICareerFrameworkService careerFrameworkService)
    {
        _careerFrameworkService = careerFrameworkService;
    }

    [HttpGet("industries/{industryId:int}/majors")]
    public async Task<IActionResult> GetMajorsAsync(int industryId, CancellationToken cancellationToken = default)
    {
        var majors = await _careerFrameworkService.GetMajorsByIndustryAsync(industryId, cancellationToken);
        return Ok(majors);
    }

    [HttpPost("industries/{industryId:int}/majors")]
    public async Task<IActionResult> CreateMajorAsync(int industryId, [FromBody] MajorRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var major = await _careerFrameworkService.CreateMajorAsync(industryId, request, GetAdminId(), cancellationToken);
            return CreatedAtRoute("GetMajorById", new { id = major.Id }, major);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("majors/{id:int}", Name = "GetMajorById")]
    public async Task<IActionResult> GetMajorByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var major = await _careerFrameworkService.GetMajorByIdAsync(id, cancellationToken);
        return major == null ? NotFound() : Ok(major);
    }

    [HttpPut("majors/{id:int}")]
    public async Task<IActionResult> UpdateMajorAsync(int id, [FromBody] MajorRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _careerFrameworkService.UpdateMajorAsync(id, request, GetAdminId(), cancellationToken);
            if (updated == null)
            {
                return NotFound();
            }

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("majors/{id:int}")]
    public async Task<IActionResult> DeleteMajorAsync(int id, CancellationToken cancellationToken = default)
    {
        var deleted = await _careerFrameworkService.SoftDeleteMajorAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

