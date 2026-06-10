using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistant.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeStepIngredientMarkup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstructionMarkup",
                table: "RecipeSteps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarkerToken",
                table: "RecipeIngredients",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_RecipeId_MarkerToken",
                table: "RecipeIngredients",
                columns: new[] { "RecipeId", "MarkerToken" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecipeIngredients_RecipeId_MarkerToken",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "InstructionMarkup",
                table: "RecipeSteps");

            migrationBuilder.DropColumn(
                name: "MarkerToken",
                table: "RecipeIngredients");
        }
    }
}
