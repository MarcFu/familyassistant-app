using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistant.Migrations
{
    /// <inheritdoc />
    public partial class AddIntervalMonthsToSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntervalMonths",
                table: "ChoreSchedules",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntervalMonths",
                table: "ChoreSchedules");
        }
    }
}
