using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SchoolAdmission.Infrastructure.Persistence;

/// <summary>Used only by the <c>dotnet ef</c> tooling. Generating migrations does not need a live database.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AdmissionDbContext>
{
    public AdmissionDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var url = Environment.GetEnvironmentVariable(ConnectionStringResolver.DatabaseUrlKey);
            connectionString = string.IsNullOrWhiteSpace(url)
                ? "Host=localhost;Port=5432;Database=admission;Username=admission;Password=admission"
                : ConnectionStringResolver.Normalize(url);
        }

        var options = new DbContextOptionsBuilder<AdmissionDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AdmissionDbContext(options);
    }
}
