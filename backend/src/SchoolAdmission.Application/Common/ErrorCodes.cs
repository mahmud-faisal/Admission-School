namespace SchoolAdmission.Application.Common;

/// <summary>Machine-readable <c>code</c> values used in ProblemDetails responses (DESIGN §8.5).</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string InvalidStudentId = "INVALID_STUDENT_ID";
    public const string NotFound = "NOT_FOUND";
    public const string Forbidden = "FORBIDDEN";
    public const string SeatsFull = "SEATS_FULL";
    public const string DuplicateApplication = "DUPLICATE_APPLICATION";
    public const string HoldExpired = "HOLD_EXPIRED";
    public const string HoldCancelled = "HOLD_CANCELLED";
    public const string HoldAlreadyConfirmed = "HOLD_ALREADY_CONFIRMED";
    public const string Conflict = "CONFLICT";
    public const string UnsupportedApiVersion = "UNSUPPORTED_API_VERSION";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string InternalError = "INTERNAL_ERROR";
}

/// <summary>Ready-made business errors returned by handlers.</summary>
public static class Errors
{
    public static Error ClassNotFound(string classId) =>
        new(ErrorCodes.NotFound, $"Class '{classId}' was not found.", ErrorKind.NotFound);

    public static Error HoldNotFound(string holdId) =>
        new(ErrorCodes.NotFound, $"Application '{holdId}' was not found.", ErrorKind.NotFound);

    public static readonly Error Forbidden =
        new(ErrorCodes.Forbidden, "This application belongs to a different student.", ErrorKind.Forbidden);

    public static readonly Error SeatsFull =
        new(ErrorCodes.SeatsFull, "No seat is available in this class.", ErrorKind.Conflict);

    public static readonly Error DuplicateApplication =
        new(ErrorCodes.DuplicateApplication, "The student already has an active application or a confirmed admission in this class.", ErrorKind.Conflict);

    public static readonly Error HoldExpired =
        new(ErrorCodes.HoldExpired, "The application has expired and its seat was released.", ErrorKind.Conflict);

    public static readonly Error HoldCancelled =
        new(ErrorCodes.HoldCancelled, "The application was cancelled.", ErrorKind.Conflict);

    public static readonly Error HoldAlreadyConfirmed =
        new(ErrorCodes.HoldAlreadyConfirmed, "The admission is already confirmed and cannot be cancelled.", ErrorKind.Conflict);

    public static readonly Error ConcurrentUpdate =
        new(ErrorCodes.Conflict, "The application changed while the request was being processed. Please try again.", ErrorKind.Conflict);
}
