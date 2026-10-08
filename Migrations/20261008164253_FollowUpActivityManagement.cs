using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcxiomCRM.Migrations
{
    /// <inheritdoc />
    public partial class FollowUpActivityManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "FollowUps",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Activities",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_Type",
                table: "FollowUps",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Status",
                table: "Activities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Type",
                table: "Activities",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FollowUps_Type",
                table: "FollowUps");

            migrationBuilder.DropIndex(
                name: "IX_Activities_Status",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_Type",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Activities");
        }
    }
}
