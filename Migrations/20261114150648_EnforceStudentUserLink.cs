using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarksApi.Migrations
{
    /// <inheritdoc />
    public partial class EnforceStudentUserLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_users_student_requires_student_id",
                table: "users",
                sql: "role <> 'Student' OR student_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_student_requires_student_id",
                table: "users");
        }
    }
}
