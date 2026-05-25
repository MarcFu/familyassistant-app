using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistant.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Chores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreditsReward = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Persons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HaEntityId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    Credits = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceEntities = table.Column<string>(type: "TEXT", nullable: false),
                    HaTodoEntityId = table.Column<string>(type: "TEXT", nullable: true),
                    EntityPicture = table.Column<string>(type: "TEXT", nullable: true),
                    IsPaused = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChoreSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultPersonId = table.Column<int>(type: "INTEGER", nullable: true),
                    Rhythm = table.Column<int>(type: "INTEGER", nullable: false),
                    TimesPerWeek = table.Column<int>(type: "INTEGER", nullable: true),
                    SpecificDays = table.Column<int>(type: "INTEGER", nullable: true),
                    TimesPerDay = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeLabel = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChoreSchedules_Chores_ChoreId",
                        column: x => x.ChoreId,
                        principalTable: "Chores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChoreSchedules_Persons_DefaultPersonId",
                        column: x => x.DefaultPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InternetRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    RuleType = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    WindowStart = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    WindowEnd = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    DailyMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    ApplicableDays = table.Column<int>(type: "INTEGER", nullable: false),
                    CreditCostPerExtraMinute = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternetRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternetRules_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChoreTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    OccurrenceIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurrenceLabel = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultPersonId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClaimedByPersonId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedByPersonId = table.Column<int>(type: "INTEGER", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConfirmedByPersonId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreditsAwarded = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChoreTasks_ChoreSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "ChoreSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChoreTasks_Chores_ChoreId",
                        column: x => x.ChoreId,
                        principalTable: "Chores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChoreTasks_Persons_ClaimedByPersonId",
                        column: x => x.ClaimedByPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChoreTasks_Persons_CompletedByPersonId",
                        column: x => x.CompletedByPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChoreTasks_Persons_ConfirmedByPersonId",
                        column: x => x.ConfirmedByPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChoreTasks_Persons_DefaultPersonId",
                        column: x => x.DefaultPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CreditTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    ChoreTaskId = table.Column<int>(type: "INTEGER", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditTransactions_ChoreTasks_ChoreTaskId",
                        column: x => x.ChoreTaskId,
                        principalTable: "ChoreTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreditTransactions_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissedCreditsLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChoreTaskId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreditsNotEarned = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<int>(type: "INTEGER", nullable: false),
                    WasPaused = table.Column<bool>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissedCreditsLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissedCreditsLogs_ChoreSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "ChoreSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissedCreditsLogs_ChoreTasks_ChoreTaskId",
                        column: x => x.ChoreTaskId,
                        principalTable: "ChoreTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissedCreditsLogs_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreSchedules_ChoreId",
                table: "ChoreSchedules",
                column: "ChoreId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreSchedules_DefaultPersonId",
                table: "ChoreSchedules",
                column: "DefaultPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreTasks_ChoreId",
                table: "ChoreTasks",
                column: "ChoreId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreTasks_ClaimedByPersonId",
                table: "ChoreTasks",
                column: "ClaimedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreTasks_CompletedByPersonId",
                table: "ChoreTasks",
                column: "CompletedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreTasks_ConfirmedByPersonId",
                table: "ChoreTasks",
                column: "ConfirmedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreTasks_DefaultPersonId",
                table: "ChoreTasks",
                column: "DefaultPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreTasks_ScheduleId_DueDate_OccurrenceIndex",
                table: "ChoreTasks",
                columns: new[] { "ScheduleId", "DueDate", "OccurrenceIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChoreTasks_Status_DueDate",
                table: "ChoreTasks",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_ChoreTaskId",
                table: "CreditTransactions",
                column: "ChoreTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_PersonId_Timestamp",
                table: "CreditTransactions",
                columns: new[] { "PersonId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_InternetRules_PersonId",
                table: "InternetRules",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_MissedCreditsLogs_ChoreTaskId",
                table: "MissedCreditsLogs",
                column: "ChoreTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_MissedCreditsLogs_PersonId_Date",
                table: "MissedCreditsLogs",
                columns: new[] { "PersonId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_MissedCreditsLogs_ScheduleId",
                table: "MissedCreditsLogs",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Persons_HaEntityId",
                table: "Persons",
                column: "HaEntityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditTransactions");

            migrationBuilder.DropTable(
                name: "InternetRules");

            migrationBuilder.DropTable(
                name: "MissedCreditsLogs");

            migrationBuilder.DropTable(
                name: "ChoreTasks");

            migrationBuilder.DropTable(
                name: "ChoreSchedules");

            migrationBuilder.DropTable(
                name: "Chores");

            migrationBuilder.DropTable(
                name: "Persons");
        }
    }
}
