namespace SchoolAdmission.Application.Common;

public enum ErrorKind
{
    Validation,
    NotFound,
    Forbidden,
    Conflict,
    Unexpected,
}

/// <summary>An expected business failure. <see cref="Code"/> is the machine-readable ProblemDetails <c>code</c>.</summary>
public sealed record Error(string Code, string Message, ErrorKind Kind);

public class Result
{
    protected Result(Error? error) => Error = error;

    public bool IsSuccess => Error is null;

    public Error? Error { get; }

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static Result<T> Success<T>(T value) => new(value, null);

    public static Result<T> Failure<T>(Error error) => new(default, error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    internal Result(T? value, Error? error)
        : base(error) => Value = value;

    /// <summary>Set when <see cref="Result.IsSuccess"/> is true.</summary>
    public T? Value { get; }

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
