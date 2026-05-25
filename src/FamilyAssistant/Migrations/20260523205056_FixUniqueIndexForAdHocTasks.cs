using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistant.Migrations
{
    /// <inheritdoc />
    public partial class FixUniqueIndexForAdHocTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChoreTasks_ScheduleId_DueDate_OccurrenceIndex",
                table: "ChoreTasks");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreTasks_ScheduleId_DueDate_OccurrenceIndex",
                table: "ChoreTasks",
                columns: new[] { "ScheduleId", "DueDate", "OccurrenceIndex" },
                unique: true,
                filter: "[ScheduleId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChoreTasks_ScheduleId_DueDate_OccurrenceIndex",
                table: "ChoreTasks");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreTasks_ScheduleId_DueDate_OccurrenceIndex",
                table: "ChoreTasks",
                columns: new[] { "ScheduleId", "DueDate", "OccurrenceIndex" },
                unique: true);
        }
    }
}
