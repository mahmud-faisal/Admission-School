using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.ErrorHandling;
using SchoolAdmission.Application.Holds;
using SchoolAdmission.Application.Holds.GetMyHolds;

namespace SchoolAdmission.Api.Controllers;

/// <summary>The current student's applications.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("me")]
public sealed class MeController(ISender sender) : ControllerBase
{
    /// <summary>All applications of this student (every status, newest first).</summary>
    [HttpGet("holds")]
    [ProducesResponseType<IReadOnlyList<MyHoldDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> MyHolds([FromHeader(Name = Headers.StudentId)] string? studentId, CancellationToken ct)
    {
        var result = await sender.Send(new GetMyHoldsQuery(studentId), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error!);
    }
}
