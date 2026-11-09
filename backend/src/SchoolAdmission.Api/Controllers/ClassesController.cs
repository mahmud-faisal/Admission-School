using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.ErrorHandling;
using SchoolAdmission.Application.Classes;
using SchoolAdmission.Application.Classes.CreateClass;
using SchoolAdmission.Application.Classes.GetClass;
using SchoolAdmission.Application.Classes.ListClasses;
using SchoolAdmission.Application.Holds;
using SchoolAdmission.Application.Holds.CreateHold;

namespace SchoolAdmission.Api.Controllers;

/// <summary>Classes (each shift is its own class) and applying for a seat.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("classes")]
public sealed class ClassesController(ISender sender) : ControllerBase
{
    /// <summary>Create a new class with a name and seat count.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ClassDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Create(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateClassRequest? request,
        CancellationToken ct)
    {
        // An empty body reaches the validator as null fields, so the user gets "name is required" etc.
        var result = await sender.Send(new CreateClassCommand(request?.Name, request?.TotalSeats), ct);
        return result.IsSuccess
            ? Created($"/classes/{result.Value!.Id}", result.Value)
            : this.ToProblem(result.Error!);
    }

    /// <summary>List all classes with seat counts (oldest first).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClassDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await sender.Send(new ListClassesQuery(), ct));

    /// <summary>Seat status of one class.</summary>
    [HttpGet("{classId}")]
    [ProducesResponseType<ClassDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(string classId, CancellationToken ct)
    {
        var result = await sender.Send(new GetClassQuery(classId), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error!);
    }

    /// <summary>Apply: hold a seat for the student for HOLD_DURATION_SECONDS. 409 if no seat is available.</summary>
    [HttpPost("{classId}/holds")]
    [ProducesResponseType<HoldCreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> CreateHold(
        string classId,
        [FromHeader(Name = Headers.StudentId)] string? studentId,
        CancellationToken ct)
    {
        var result = await sender.Send(new CreateHoldCommand(classId, studentId), ct);
        return result.IsSuccess
            ? Created($"/holds/{result.Value!.HoldId}", result.Value)
            : this.ToProblem(result.Error!);
    }
}
