using Microsoft.AspNetCore.Mvc;
using TheJourney.Api.Modules.Admin.CareerFramework.Dtos;
using TheJourney.Api.Modules.Admin.CareerFramework.Services;

namespace TheJourney.Api.Modules.Admin.CareerFramework.Controllers;

[Route("api/admin/skills")]
public class SkillsController : AdminAuthorizedController
{
    private readonly ICareerFrameworkService _careerFrameworkService;

    public SkillsController(ICareerFrameworkService careerFrameworkService)
    {
        _careerFrameworkService = careerFrameworkService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSkillsAsync(CancellationToken cancellationToken = default)
    {
        var skills = await _careerFrameworkService.GetSkillsAsync(cancellationToken);
        return Ok(skills);
    }

    [HttpGet("{id:int}", Name = "GetSkillById")]
    public async Task<IActionResult> GetSkillByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var skills = await _careerFrameworkService.GetSkillsAsync(cancellationToken);
        var skill = skills.FirstOrDefault(s => s.Id == id);
        return skill == null ? NotFound() : Ok(skill);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSkillAsync([FromBody] SkillRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var skill = await _careerFrameworkService.CreateSkillAsync(request, cancellationToken);
            return CreatedAtRoute("GetSkillById", new { id = skill.Id }, skill);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}

