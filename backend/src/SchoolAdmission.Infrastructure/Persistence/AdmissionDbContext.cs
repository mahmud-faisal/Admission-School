using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Domain.Classes;
using SchoolAdmission.Domain.Holds;

namespace SchoolAdmission.Infrastructure.Persistence;

/// <summary>
/// EF Core owns the schema, migrations, class inserts and transactions. All locking, expiry and
/// seat-count logic is raw parameterized SQL in the repositories (DESIGN §6).
/// </summary>
public sealed class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options) : DbContext(options)
{
    public DbSet<Class> Classes => Set<Class>();

    public DbSet<Hold> Holds => Set<Hold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdmissionDbContext).Assembly);
}
