using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistant.Migrations
{
    /// <inheritdoc />
    public partial class AddChoreIconColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IconColor",
                table: "Chores",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IconColor",
                table: "Chores");
        }
    }
}
