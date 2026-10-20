using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HamiChap_MES_ERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesJobTypeAndSpecifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JobSpecificationJson",
                table: "SalesRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "JobType",
                table: "SalesRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobSpecificationJson",
                table: "SalesRequests");

            migrationBuilder.DropColumn(
                name: "JobType",
                table: "SalesRequests");
        }
    }
}
