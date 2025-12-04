using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheJourney.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSummaryToStudents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Students",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Students");
        }
    }
}
