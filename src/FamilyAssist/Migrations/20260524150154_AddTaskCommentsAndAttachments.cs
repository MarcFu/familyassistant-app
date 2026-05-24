using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssist.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskCommentsAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChoreTaskId = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskComments_ChoreTasks_ChoreTaskId",
                        column: x => x.ChoreTaskId,
                        principalTable: "ChoreTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskComments_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaskCommentId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAttachments_TaskComments_TaskCommentId",
                        column: x => x.TaskCommentId,
                        principalTable: "TaskComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachments_TaskCommentId",
                table: "TaskAttachments",
                column: "TaskCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_ChoreTaskId_CreatedAt",
                table: "TaskComments",
                columns: new[] { "ChoreTaskId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_PersonId",
                table: "TaskComments",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskAttachments");

            migrationBuilder.DropTable(
                name: "TaskComments");
        }
    }
}
