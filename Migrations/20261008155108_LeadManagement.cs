using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcxiomCRM.Migrations
{
    /// <inheritdoc />
    public partial class LeadManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Leads");

            migrationBuilder.RenameColumn(
                name: "LastName",
                table: "Leads",
                newName: "LeadCode");

            migrationBuilder.RenameColumn(
                name: "EstimatedValue",
                table: "Leads",
                newName: "ConvertedDate");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Leads",
                newName: "CreatedDate");

            migrationBuilder.RenameColumn(
                name: "Company",
                table: "Leads",
                newName: "CompanyName");

            migrationBuilder.AddColumn<int>(
                name: "LeadId",
                table: "Opportunities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "Leads",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConvertedOpportunityId",
                table: "Leads",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Leads",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedValue",
                table: "Leads",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeadName",
                table: "Leads",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_LeadId",
                table: "Opportunities",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ConvertedOpportunityId",
                table: "Leads",
                column: "ConvertedOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_LeadCode",
                table: "Leads",
                column: "LeadCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_LeadName",
                table: "Leads",
                column: "LeadName");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Phone",
                table: "Leads",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Source",
                table: "Leads",
                column: "Source");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Opportunities_ConvertedOpportunityId",
                table: "Leads",
                column: "ConvertedOpportunityId",
                principalTable: "Opportunities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunities_Leads_LeadId",
                table: "Opportunities",
                column: "LeadId",
                principalTable: "Leads",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Opportunities_ConvertedOpportunityId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunities_Leads_LeadId",
                table: "Opportunities");

            migrationBuilder.DropIndex(
                name: "IX_Opportunities_LeadId",
                table: "Opportunities");

            migrationBuilder.DropIndex(
                name: "IX_Leads_ConvertedOpportunityId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_LeadCode",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_LeadName",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_Phone",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_Source",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "LeadId",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "ConvertedOpportunityId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ExpectedValue",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "LeadName",
                table: "Leads");

            migrationBuilder.RenameColumn(
                name: "LeadCode",
                table: "Leads",
                newName: "LastName");

            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "Leads",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "ConvertedDate",
                table: "Leads",
                newName: "EstimatedValue");

            migrationBuilder.RenameColumn(
                name: "CompanyName",
                table: "Leads",
                newName: "Company");

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "Leads",
                type: "TEXT",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Leads",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
