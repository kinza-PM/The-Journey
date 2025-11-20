using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TheJourney.Api.Modules.Mobile.Assessment.Dtos;
using TheJourney.Api.Modules.Mobile.Assessment.Models;
using TheJourney.Api.Modules.Mobile.Assessment.Services;

namespace TheJourney.Api.Modules.Mobile.Assessment.Controllers;

[Route("api/mobile/assessments")]
public class AssessmentsController : StudentAuthorizedController
{
    private readonly IAssessmentService _assessmentService;

    public AssessmentsController(IAssessmentService assessmentService)
    {
        _assessmentService = assessmentService;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartAssessmentAsync([FromBody] StartAssessmentRequestDto request, CancellationToken cancellationToken = default)
    {
        var studentId = GetStudentId();
        if (studentId == null)
        {
            return Unauthorized();
        }

        try
        {
            var assessment = await _assessmentService.StartAssessmentAsync(studentId.Value, request, cancellationToken);
            return Ok(new
            {
                assessment.Id,
                Template = assessment.AssessmentTemplate
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{assessmentId:int}/answers")]
    public async Task<IActionResult> SaveAnswersAsync(int assessmentId, [FromBody] AssessmentAnswerRequestDto request, CancellationToken cancellationToken = default)
    {
        var studentId = GetStudentId();
        if (studentId == null)
        {
            return Unauthorized();
        }

        try
        {
            await _assessmentService.SaveAnswersAsync(studentId.Value, assessmentId, request, cancellationToken);
            return Ok(new { message = "Answers saved." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{assessmentId:int}/submit")]
    public async Task<IActionResult> SubmitAssessmentAsync(int assessmentId, CancellationToken cancellationToken = default)
    {
        var studentId = GetStudentId();
        if (studentId == null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _assessmentService.SubmitAssessmentAsync(studentId.Value, assessmentId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{assessmentId:int}")]
    public async Task<IActionResult> GetAssessmentAsync(int assessmentId, CancellationToken cancellationToken = default)
    {
        var studentId = GetStudentId();
        if (studentId == null)
        {
            return Unauthorized();
        }

        var assessment = await _assessmentService.GetAssessmentWithTemplateAsync(studentId.Value, assessmentId, cancellationToken);
        if (assessment == null)
        {
            return NotFound(new { message = "Assessment not found." });
        }

        return Ok(new
        {
            assessment.Id,
            assessment.JobRoleId,
            assessment.Status,
            assessment.StartedAt,
            assessment.CompletedAt,
            assessment.FitScore,
            Template = assessment.AssessmentTemplate == null ? null : new
            {
                assessment.AssessmentTemplate.Id,
                assessment.AssessmentTemplate.Version,
                SkillMatrix = assessment.AssessmentTemplate.SkillMatrix?.Select(sm => new
                {
                    sm.Id,
                    sm.SkillId,
                    SkillName = sm.Skill?.Name,
                    sm.IsRequired,
                    sm.RequiredProficiencyLevel,
                    sm.Weight
                }),
                Questions = assessment.AssessmentTemplate.Questions?.Select(q => new
                {
                    q.Id,
                    q.QuestionText,
                    q.QuestionType,
                    q.IsRequired,
                    q.OrderIndex
                })
            }
        });
    }

    [HttpGet("{assessmentId:int}/results")]
    public async Task<IActionResult> GetAssessmentResultAsync(int assessmentId, CancellationToken cancellationToken = default)
    {
        var studentId = GetStudentId();
        if (studentId == null)
        {
            return Unauthorized();
        }

        var assessment = await _assessmentService.GetAssessmentAsync(studentId.Value, assessmentId, cancellationToken);
        if (assessment == null || string.IsNullOrWhiteSpace(assessment.FitScoreBreakdownJson))
        {
            return NotFound(new { message = "Assessment result not available yet." });
        }

        var breakdown = JsonSerializer.Deserialize<FitScoreResult>(assessment.FitScoreBreakdownJson);
        return Ok(new
        {
            assessment.Id,
            assessment.JobRoleId,
            assessment.Status,
            assessment.FitScore,
            assessment.CompletedAt,
            breakdown
        });
    }
}

