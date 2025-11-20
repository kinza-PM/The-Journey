using Microsoft.AspNetCore.Mvc;
using TheJourney.Api.Modules.Admin.CareerFramework.Dtos;
using TheJourney.Api.Modules.Admin.CareerFramework.Services;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Controllers;

[Route("api/admin")]
public class JobRolesController : AdminAuthorizedController
{
    private readonly ICareerFrameworkService _careerFrameworkService;

    public JobRolesController(ICareerFrameworkService careerFrameworkService)
    {
        _careerFrameworkService = careerFrameworkService;
    }

    [HttpGet("majors/{majorId:int}/job-roles")]
    public async Task<IActionResult> GetJobRolesAsync(int majorId, CancellationToken cancellationToken = default)
    {
        var roles = await _careerFrameworkService.GetJobRolesByMajorAsync(majorId, cancellationToken);
        return Ok(roles);
    }

    [HttpPost("majors/{majorId:int}/job-roles")]
    public async Task<IActionResult> CreateJobRoleAsync(int majorId, [FromBody] JobRoleRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var role = await _careerFrameworkService.CreateJobRoleAsync(majorId, request, GetAdminId(), cancellationToken);
            return CreatedAtRoute("GetJobRoleById", new { id = role.Id }, role);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("job-roles/{id:int}", Name = "GetJobRoleById")]
    public async Task<IActionResult> GetJobRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = await _careerFrameworkService.GetJobRoleByIdAsync(id, cancellationToken);
        return role == null ? NotFound() : Ok(role);
    }

    [HttpPut("job-roles/{id:int}")]
    public async Task<IActionResult> UpdateJobRoleAsync(int id, [FromBody] JobRoleRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _careerFrameworkService.UpdateJobRoleAsync(id, request, GetAdminId(), cancellationToken);
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

    [HttpDelete("job-roles/{id:int}")]
    public async Task<IActionResult> DeleteJobRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        var deleted = await _careerFrameworkService.SoftDeleteJobRoleAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

