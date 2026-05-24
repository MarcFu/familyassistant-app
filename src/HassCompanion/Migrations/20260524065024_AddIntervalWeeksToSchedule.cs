using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HassCompanion.Migrations
{
    /// <inheritdoc />
    public partial class AddIntervalWeeksToSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntervalWeeks",
                table: "ChoreSchedules",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntervalWeeks",
                table: "ChoreSchedules");
        }
    }
}
