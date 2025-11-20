using Microsoft.AspNetCore.Mvc;
using TheJourney.Api.Modules.Admin.CareerFramework.Dtos;
using TheJourney.Api.Modules.Admin.CareerFramework.Services;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Controllers;

[Route("api/admin/training-resources")]
public class TrainingResourcesController : AdminAuthorizedController
{
    private readonly ICareerFrameworkService _careerFrameworkService;

    public TrainingResourcesController(ICareerFrameworkService careerFrameworkService)
    {
        _careerFrameworkService = careerFrameworkService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTrainingResourcesAsync(CancellationToken cancellationToken = default)
    {
        var resources = await _careerFrameworkService.GetTrainingResourcesAsync(cancellationToken);
        return Ok(resources);
    }

    [HttpGet("{id:int}", Name = "GetTrainingResourceById")]
    public async Task<IActionResult> GetTrainingResourceByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var resources = await _careerFrameworkService.GetTrainingResourcesAsync(cancellationToken);
        var resource = resources.FirstOrDefault(r => r.Id == id);
        return resource == null ? NotFound() : Ok(resource);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTrainingResourceAsync([FromBody] TrainingResourceRequestDto request, CancellationToken cancellationToken = default)
    {
        var resource = await _careerFrameworkService.CreateTrainingResourceAsync(request, cancellationToken);
        return CreatedAtRoute("GetTrainingResourceById", new { id = resource.Id }, resource);
    }
}

