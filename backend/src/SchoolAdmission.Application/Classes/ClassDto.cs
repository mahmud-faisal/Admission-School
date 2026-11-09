using SchoolAdmission.Application.Abstractions.Persistence;

namespace SchoolAdmission.Application.Classes;

public sealed record ClassDto(
    string Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats,
    DateTime CreatedAt)
{
    public static ClassDto From(ClassSeatsRow row) =>
        new(row.Id, row.Name, row.TotalSeats, row.HeldSeats, row.ConfirmedSeats, row.AvailableSeats, row.CreatedAt);
}
