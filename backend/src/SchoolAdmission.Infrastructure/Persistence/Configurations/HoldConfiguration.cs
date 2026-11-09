using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolAdmission.Domain.Classes;
using SchoolAdmission.Domain.Holds;

namespace SchoolAdmission.Infrastructure.Persistence.Configurations;

internal sealed class HoldConfiguration : IEntityTypeConfiguration<Hold>
{
    /// <summary>
    /// Invariant I2. A partial-index predicate must be IMMUTABLE, so it cannot use now(): an expired-but-unswept HELD
    /// row is still inside it. The create-hold transaction marks the class's stale rows EXPIRED under the class lock
    /// before it checks and inserts (DESIGN §3.2).
    /// </summary>
    public const string ActiveUniqueIndexName = "ux_holds_class_student_active";

    public void Configure(EntityTypeBuilder<Hold> b)
    {
        b.ToTable("holds", t =>
        {
            t.HasCheckConstraint("ck_holds_status", "status IN ('HELD','CONFIRMED','CANCELLED','EXPIRED')");
            t.HasCheckConstraint("ck_holds_student_id", "student_id ~ '^[0-9]{17}$'");
            t.HasCheckConstraint("ck_holds_confirmed_at", "status <> 'CONFIRMED' OR confirmed_at IS NOT NULL");
        });

        b.HasKey(x => x.Id).HasName("pk_holds");

        b.Property(x => x.Id).HasColumnName("id").HasMaxLength(40).ValueGeneratedNever();
        b.Property(x => x.ClassId).HasColumnName("class_id").HasMaxLength(40).IsRequired();
        b.Property(x => x.StudentId).HasColumnName("student_id").HasMaxLength(17).IsRequired();
        b.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(v => HoldStatusNames.ToDb(v), s => HoldStatusNames.FromDb(s));
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz");
        b.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamptz");
        b.Property(x => x.CancelledAt).HasColumnName("cancelled_at").HasColumnType("timestamptz");
        b.Property(x => x.ExpiredAt).HasColumnName("expired_at").HasColumnType("timestamptz");

        b.HasOne<Class>()
            .WithMany()
            .HasForeignKey(x => x.ClassId)
            .HasConstraintName("fk_holds_classes_class_id")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.ClassId, x.StudentId })
            .IsUnique()
            .HasFilter("status IN ('HELD','CONFIRMED')")
            .HasDatabaseName(ActiveUniqueIndexName);

        b.HasIndex(x => new { x.ClassId, x.Status, x.ExpiresAt }).HasDatabaseName("ix_holds_class_status_expires");

        b.HasIndex(x => new { x.StudentId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_holds_student_created");

        b.HasIndex(x => x.ExpiresAt)
            .HasFilter("status = 'HELD'")
            .HasDatabaseName("ix_holds_held_expires");
    }
}
