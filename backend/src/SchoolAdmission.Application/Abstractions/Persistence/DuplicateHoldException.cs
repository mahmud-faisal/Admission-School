namespace SchoolAdmission.Application.Abstractions.Persistence;

/// <summary>
/// Thrown by the repository when the partial unique index <c>ux_holds_class_student_active</c> rejects an insert.
/// Defined here so handlers can map it to DUPLICATE_APPLICATION without depending on Npgsql.
/// </summary>
public sealed class DuplicateHoldException : Exception
{
    public DuplicateHoldException()
    {
    }

    public DuplicateHoldException(string message)
        : base(message)
    {
    }

    public DuplicateHoldException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
