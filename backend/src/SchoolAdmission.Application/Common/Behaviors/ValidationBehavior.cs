using FluentValidation;
using MediatR;

namespace SchoolAdmission.Application.Common.Behaviors;

/// <summary>
/// Runs every validator for the request and throws <see cref="ValidationException"/> with all failures.
/// The API turns that into a 400 ProblemDetails (INVALID_STUDENT_ID if any failure has that code, else VALIDATION_FAILED).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var list = validators as IValidator<TRequest>[] ?? validators.ToArray();
        if (list.Length > 0)
        {
            var context = new ValidationContext<TRequest>(request);
            var results = await Task.WhenAll(list.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();
            if (failures.Count > 0)
            {
                throw new ValidationException(failures);
            }
        }

        return await next(cancellationToken);
    }
}
