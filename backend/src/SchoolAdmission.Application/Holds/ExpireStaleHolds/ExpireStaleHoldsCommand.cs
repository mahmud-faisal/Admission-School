using FluentValidation;
using MediatR;

namespace SchoolAdmission.Application.Holds.ExpireStaleHolds;

/// <summary>Internal command sent by the background sweeper. Returns the number of holds marked EXPIRED.</summary>
public sealed record ExpireStaleHoldsCommand(int BatchSize) : IRequest<int>;

public sealed class ExpireStaleHoldsValidator : AbstractValidator<ExpireStaleHoldsCommand>
{
    public ExpireStaleHoldsValidator() => RuleFor(x => x.BatchSize).InclusiveBetween(1, 10_000);
}
