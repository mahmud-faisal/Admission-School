namespace SchoolAdmission.Application.Abstractions;

/// <summary>Generates opaque public IDs: "c_" / "h_" + 32 lowercase hex characters.</summary>
public interface IIdGenerator
{
    string NewClassId();

    string NewHoldId();
}
