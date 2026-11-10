using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarksApi.Migrations
{
    /// <inheritdoc />
    public partial class AlignSection7AndApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_enrollments_courses_course_id",
                table: "enrollments");

            migrationBuilder.DropForeignKey(
                name: "fk_enrollments_students_student_id",
                table: "enrollments");

            migrationBuilder.DropForeignKey(
                name: "fk_users_students_student_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_student_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_students_last_name_first_name",
                table: "students");

            migrationBuilder.DropPrimaryKey(
                name: "pk_enrollments",
                table: "enrollments");

            migrationBuilder.DropIndex(
                name: "ix_courses_course_code",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "first_name",
                table: "students");

            migrationBuilder.DropColumn(
                name: "course_code",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "name",
                table: "courses");

            migrationBuilder.RenameColumn(
                name: "student_number",
                table: "students",
                newName: "registration_no");

            migrationBuilder.RenameColumn(
                name: "last_name",
                table: "students",
                newName: "full_name");

            migrationBuilder.RenameIndex(
                name: "ix_students_student_number",
                table: "students",
                newName: "ix_students_registration_no");

            migrationBuilder.AlterColumn<string>(
                name: "username",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at_utc",
                table: "students",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "students",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "id",
                table: "enrollments",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "enrolled_at_utc",
                table: "enrollments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "courses",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at_utc",
                table: "courses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "courses",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "pk_enrollments",
                table: "enrollments",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_users_student_id",
                table: "users",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_students_email",
                table: "students",
                column: "email",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_email_lowercase",
                table: "students",
                sql: "email = lower(email)");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_student_id_course_id",
                table: "enrollments",
                columns: new[] { "student_id", "course_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_courses_code",
                table: "courses",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_enrollments_courses_course_id",
                table: "enrollments",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_enrollments_students_student_id",
                table: "enrollments",
                column: "student_id",
                principalTable: "students",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_users_students_student_id",
                table: "users",
                column: "student_id",
                principalTable: "students",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_enrollments_courses_course_id",
                table: "enrollments");

            migrationBuilder.DropForeignKey(
                name: "fk_enrollments_students_student_id",
                table: "enrollments");

            migrationBuilder.DropForeignKey(
                name: "fk_users_students_student_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_student_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_students_email",
                table: "students");

            migrationBuilder.DropCheckConstraint(
                name: "ck_students_email_lowercase",
                table: "students");

            migrationBuilder.DropPrimaryKey(
                name: "pk_enrollments",
                table: "enrollments");

            migrationBuilder.DropIndex(
                name: "ix_enrollments_student_id_course_id",
                table: "enrollments");

            migrationBuilder.DropIndex(
                name: "ix_courses_code",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "created_at_utc",
                table: "students");

            migrationBuilder.DropColumn(
                name: "email",
                table: "students");

            migrationBuilder.DropColumn(
                name: "id",
                table: "enrollments");

            migrationBuilder.DropColumn(
                name: "enrolled_at_utc",
                table: "enrollments");

            migrationBuilder.DropColumn(
                name: "code",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "created_at_utc",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "title",
                table: "courses");

            migrationBuilder.RenameColumn(
                name: "registration_no",
                table: "students",
                newName: "student_number");

            migrationBuilder.RenameColumn(
                name: "full_name",
                table: "students",
                newName: "last_name");

            migrationBuilder.RenameIndex(
                name: "ix_students_registration_no",
                table: "students",
                newName: "ix_students_student_number");

            migrationBuilder.AlterColumn<string>(
                name: "username",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "first_name",
                table: "students",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "course_code",
                table: "courses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "courses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "pk_enrollments",
                table: "enrollments",
                columns: new[] { "student_id", "course_id" });

            migrationBuilder.CreateIndex(
                name: "ix_users_student_id",
                table: "users",
                column: "student_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_students_last_name_first_name",
                table: "students",
                columns: new[] { "last_name", "first_name" });

            migrationBuilder.CreateIndex(
                name: "ix_courses_course_code",
                table: "courses",
                column: "course_code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_enrollments_courses_course_id",
                table: "enrollments",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_enrollments_students_student_id",
                table: "enrollments",
                column: "student_id",
                principalTable: "students",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_users_students_student_id",
                table: "users",
                column: "student_id",
                principalTable: "students",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
