using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcxiomCRM.Migrations
{
    /// <inheritdoc />
    public partial class OpportunityManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Opportunities",
                newName: "OpportunityName");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Opportunities",
                newName: "CreatedDate");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Opportunities",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Opportunities",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_ExpectedCloseDate",
                table: "Opportunities",
                column: "ExpectedCloseDate");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_OpportunityName",
                table: "Opportunities",
                column: "OpportunityName");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_Status",
                table: "Opportunities",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Opportunities_ExpectedCloseDate",
                table: "Opportunities");

            migrationBuilder.DropIndex(
                name: "IX_Opportunities_OpportunityName",
                table: "Opportunities");

            migrationBuilder.DropIndex(
                name: "IX_Opportunities_Status",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Opportunities");

            migrationBuilder.RenameColumn(
                name: "OpportunityName",
                table: "Opportunities",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "Opportunities",
                newName: "CreatedAt");
        }
    }
}
