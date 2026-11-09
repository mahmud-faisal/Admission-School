using MediatR;
using SchoolAdmission.Application.Abstractions;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Domain.Classes;

namespace SchoolAdmission.Application.Classes.CreateClass;

public sealed class CreateClassHandler(IClassRepository classes, IUnitOfWork unitOfWork, IIdGenerator ids)
    : IRequestHandler<CreateClassCommand, Result<ClassDto>>
{
    public async Task<Result<ClassDto>> Handle(CreateClassCommand request, CancellationToken cancellationToken)
    {
        // The validator guarantees both values are present and in range.
        var entity = Class.Create(ids.NewClassId(), request.Name!, request.TotalSeats!.Value);

        classes.Add(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read so created_at (DB default) and the seat counts come from the database.
        var row = await classes.GetSeatsAsync(entity.Id, cancellationToken)
                  ?? throw new InvalidOperationException($"Class '{entity.Id}' was not found right after insert.");

        return ClassDto.From(row);
    }
}
