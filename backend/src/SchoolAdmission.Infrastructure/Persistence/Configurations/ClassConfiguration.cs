using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolAdmission.Domain.Classes;

namespace SchoolAdmission.Infrastructure.Persistence.Configurations;

internal sealed class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> b)
    {
        b.ToTable("classes", t =>
        {
            t.HasCheckConstraint("ck_classes_total_seats", $"total_seats BETWEEN {Class.MinSeats} AND {Class.MaxSeats}");
            t.HasCheckConstraint("ck_classes_name_not_blank", "length(btrim(name)) > 0");
        });

        b.HasKey(x => x.Id).HasName("pk_classes");

        b.Property(x => x.Id).HasColumnName("id").HasMaxLength(40).ValueGeneratedNever();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(Class.MaxNameLength).IsRequired();
        b.Property(x => x.TotalSeats).HasColumnName("total_seats").IsRequired();

        // DB clock: EF omits the column on insert (CLR default value), so PostgreSQL's now() is used.
        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        b.HasIndex(x => new { x.CreatedAt, x.Id }).HasDatabaseName("ix_classes_created_at");
    }
}
