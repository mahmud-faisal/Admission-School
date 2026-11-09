namespace SchoolAdmission.Domain.Holds;

/// <summary>
/// The facts needed to explain why a conditional UPDATE matched no row. <c>DbNow</c> is the database clock.
/// Lives in Domain (not Application) because <see cref="HoldRules"/> consumes it.
/// </summary>
public sealed record HoldSnapshot(string Id, string StudentId, HoldStatus Status, DateTime ExpiresAt, DateTime DbNow);
