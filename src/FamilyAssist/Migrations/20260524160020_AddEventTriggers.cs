using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssist.Migrations
{
    /// <inheritdoc />
    public partial class AddEventTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventTriggers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    TriggerState = table.Column<string>(type: "TEXT", nullable: false),
                    ChoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DebounceMins = table.Column<int>(type: "INTEGER", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventTriggers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventTriggers_Chores_ChoreId",
                        column: x => x.ChoreId,
                        principalTable: "Chores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventTriggers_ChoreId",
                table: "EventTriggers",
                column: "ChoreId");

            migrationBuilder.CreateIndex(
                name: "IX_EventTriggers_EntityId",
                table: "EventTriggers",
                column: "EntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventTriggers");
        }
    }
}
