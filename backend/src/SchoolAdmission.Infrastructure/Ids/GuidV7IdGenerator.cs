using SchoolAdmission.Application.Abstractions;

namespace SchoolAdmission.Infrastructure.Ids;

/// <summary>Time-ordered UUIDv7 (good index locality, 74 random bits): "c_"/"h_" + 32 lowercase hex.</summary>
internal sealed class GuidV7IdGenerator : IIdGenerator
{
    public string NewClassId() => "c_" + Guid.CreateVersion7().ToString("N");

    public string NewHoldId() => "h_" + Guid.CreateVersion7().ToString("N");
}
