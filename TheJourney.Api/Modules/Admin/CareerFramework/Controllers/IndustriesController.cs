using Microsoft.AspNetCore.Mvc;
using TheJourney.Api.Modules.Admin.CareerFramework.Dtos;
using TheJourney.Api.Modules.Admin.CareerFramework.Services;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Controllers;

[Route("api/admin/industries")]
public class IndustriesController : AdminAuthorizedController
{
    private readonly ICareerFrameworkService _careerFrameworkService;

    public IndustriesController(ICareerFrameworkService careerFrameworkService)
    {
        _careerFrameworkService = careerFrameworkService;
    }

    [HttpGet]
    public async Task<IActionResult> GetIndustriesAsync([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var industries = await _careerFrameworkService.GetIndustriesAsync(includeInactive, cancellationToken);
        return Ok(industries);
    }

    [HttpGet("{id:int}", Name = "GetIndustryById")]
    public async Task<IActionResult> GetIndustryAsync(int id, CancellationToken cancellationToken = default)
    {
        var industry = await _careerFrameworkService.GetIndustryByIdAsync(id, cancellationToken);
        if (industry == null)
        {
            return NotFound();
        }

        return Ok(industry);
    }

    [HttpPost]
    public async Task<IActionResult> CreateIndustryAsync([FromBody] IndustryRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _careerFrameworkService.CreateIndustryAsync(request, GetAdminId(), cancellationToken);
            return CreatedAtRoute("GetIndustryById", new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateIndustryAsync(int id, [FromBody] IndustryRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _careerFrameworkService.UpdateIndustryAsync(id, request, GetAdminId(), cancellationToken);
            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteIndustryAsync(int id, CancellationToken cancellationToken = default)
    {
        var deleted = await _careerFrameworkService.SoftDeleteIndustryAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

