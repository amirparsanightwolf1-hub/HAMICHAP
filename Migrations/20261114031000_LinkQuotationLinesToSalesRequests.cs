using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HamiChap_MES_ERP.Migrations;

/// <summary>
/// Enables a single quotation to include multiple jobs, retaining the
/// original quotation-to-primary-request foreign key for older records.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261114031000_LinkQuotationLinesToSalesRequests")]
public partial class LinkQuotationLinesToSalesRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "SourceRequestId",
            table: "QuotationLines",
            type: "bigint",
            nullable: true);

        // Existing quotations receive their original request association.
        // This is data-preserving; quotation and order identifiers do not change.
        migrationBuilder.Sql(@"
UPDATE ql
SET ql.SourceRequestId = q.SalesRequestId
FROM dbo.QuotationLines AS ql
INNER JOIN dbo.Quotations AS q ON q.Id = ql.QuotationId
WHERE ql.SourceRequestId IS NULL;
");

        migrationBuilder.CreateIndex(
            name: "IX_QuotationLines_SourceRequestId",
            table: "QuotationLines",
            column: "SourceRequestId");

        migrationBuilder.AddForeignKey(
            name: "FK_QuotationLines_SalesRequests_SourceRequestId",
            table: "QuotationLines",
            column: "SourceRequestId",
            principalTable: "SalesRequests",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_QuotationLines_SalesRequests_SourceRequestId",
            table: "QuotationLines");
        migrationBuilder.DropIndex(
            name: "IX_QuotationLines_SourceRequestId",
            table: "QuotationLines");
        migrationBuilder.DropColumn(
            name: "SourceRequestId",
            table: "QuotationLines");
    }
}
