namespace SchoolAdmission.Api.Contracts;

/// <summary>Body of POST /classes. Nullable so a missing field is a clear 400, not a silent default.</summary>
/// <param name="Name">Class (shift) name, e.g. "Class 6 - Morning Shift". 1-200 characters.</param>
/// <param name="TotalSeats">Number of seats, 1-100000.</param>
public sealed record CreateClassRequest(string? Name, int? TotalSeats);
