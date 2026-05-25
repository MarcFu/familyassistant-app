using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistant.Migrations
{
    /// <inheritdoc />
    public partial class InternetRulesRefactorWithDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceEntities",
                table: "Persons");

            migrationBuilder.RenameColumn(
                name: "RuleType",
                table: "InternetRules",
                newName: "Target");

            migrationBuilder.RenameColumn(
                name: "DailyMinutes",
                table: "InternetRules",
                newName: "GracePeriodMinutes");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "InternetRules",
                newName: "RuleMode");

            migrationBuilder.AddColumn<string>(
                name: "NotifyEntity",
                table: "Persons",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "BudgetResetTime",
                table: "InternetRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DailyBudgetMinutes",
                table: "InternetRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DetectionThresholdMinutes",
                table: "InternetRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotifyAtPercent",
                table: "InternetRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PersonDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    InternetSwitchEntity = table.Column<string>(type: "TEXT", nullable: false),
                    InvertInternetSwitch = table.Column<bool>(type: "INTEGER", nullable: false),
                    DetectionEntity = table.Column<string>(type: "TEXT", nullable: true),
                    GamingBlockEntity = table.Column<string>(type: "TEXT", nullable: true),
                    InvertGamingBlock = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonDevices_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InternetRuleDevices",
                columns: table => new
                {
                    AffectedDevicesId = table.Column<int>(type: "INTEGER", nullable: false),
                    InternetRulesId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternetRuleDevices", x => new { x.AffectedDevicesId, x.InternetRulesId });
                    table.ForeignKey(
                        name: "FK_InternetRuleDevices_InternetRules_InternetRulesId",
                        column: x => x.InternetRulesId,
                        principalTable: "InternetRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InternetRuleDevices_PersonDevices_AffectedDevicesId",
                        column: x => x.AffectedDevicesId,
                        principalTable: "PersonDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InternetRuleDevices_InternetRulesId",
                table: "InternetRuleDevices",
                column: "InternetRulesId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonDevices_PersonId",
                table: "PersonDevices",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InternetRuleDevices");

            migrationBuilder.DropTable(
                name: "PersonDevices");

            migrationBuilder.DropColumn(
                name: "NotifyEntity",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "BudgetResetTime",
                table: "InternetRules");

            migrationBuilder.DropColumn(
                name: "DailyBudgetMinutes",
                table: "InternetRules");

            migrationBuilder.DropColumn(
                name: "DetectionThresholdMinutes",
                table: "InternetRules");

            migrationBuilder.DropColumn(
                name: "NotifyAtPercent",
                table: "InternetRules");

            migrationBuilder.RenameColumn(
                name: "Target",
                table: "InternetRules",
                newName: "RuleType");

            migrationBuilder.RenameColumn(
                name: "RuleMode",
                table: "InternetRules",
                newName: "Category");

            migrationBuilder.RenameColumn(
                name: "GracePeriodMinutes",
                table: "InternetRules",
                newName: "DailyMinutes");

            migrationBuilder.AddColumn<string>(
                name: "DeviceEntities",
                table: "Persons",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
