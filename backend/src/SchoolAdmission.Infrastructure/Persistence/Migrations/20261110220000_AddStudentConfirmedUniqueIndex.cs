using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolAdmission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentConfirmedUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH duplicates AS (
                    SELECT id,
                           ROW_NUMBER() OVER (PARTITION BY student_id ORDER BY confirmed_at ASC NULLS LAST, id ASC) as rn
                    FROM holds
                    WHERE status = 'CONFIRMED'
                )
                UPDATE holds
                SET status = 'CANCELLED', cancelled_at = now()
                WHERE id IN (
                    SELECT id FROM duplicates WHERE rn > 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "ux_holds_student_confirmed",
                table: "holds",
                column: "student_id",
                unique: true,
                filter: "status = 'CONFIRMED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_holds_student_confirmed",
                table: "holds");
        }
    }
}
