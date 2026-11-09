using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.ErrorHandling;
using SchoolAdmission.Application.Holds;
using SchoolAdmission.Application.Holds.CancelHold;
using SchoolAdmission.Application.Holds.ConfirmHold;
using SchoolAdmission.Application.Holds.GetHold;

namespace SchoolAdmission.Api.Controllers;

/// <summary>One application (seat hold): read, pay (confirm) and cancel.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("holds")]
public sealed class HoldsController(ISender sender) : ControllerBase
{
    /// <summary>Current status of an application. Status is HELD, CONFIRMED, EXPIRED or CANCELLED.</summary>
    [HttpGet("{holdId}")]
    [ProducesResponseType<HoldDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(string holdId, CancellationToken ct)
    {
        var result = await sender.Send(new GetHoldQuery(holdId), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error!);
    }

    /// <summary>Pay the admission fee: confirm the student's own non-expired HELD application. Idempotent.</summary>
    [HttpPost("{holdId}/confirm")]
    [ProducesResponseType<HoldConfirmedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Confirm(
        string holdId,
        [FromHeader(Name = Headers.StudentId)] string? studentId,
        CancellationToken ct)
    {
        var result = await sender.Send(new ConfirmHoldCommand(holdId, studentId), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error!);
    }

    /// <summary>Cancel the student's own HELD application and release the seat.</summary>
    [HttpDelete("{holdId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Cancel(
        string holdId,
        [FromHeader(Name = Headers.StudentId)] string? studentId,
        CancellationToken ct)
    {
        var result = await sender.Send(new CancelHoldCommand(holdId, studentId), ct);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error!);
    }
}
