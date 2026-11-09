namespace SchoolAdmission.Application.Common;

/// <summary>A request that acts on behalf of a student (from the X-Student-Id header). Used for masked logging.</summary>
public interface IStudentScopedRequest
{
    string? StudentId { get; }
}
