using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace HamiChap_MES_ERP.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261124215000_AddInvoiceVatAndTaxReporting")]
public partial class AddInvoiceVatAndTaxReporting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("CurrencyCode", "Quotations", type: "nvarchar(3)", maxLength: 3, nullable: true);
        migrationBuilder.AddColumn<decimal>("VatRate", "QuotationLines", type: "decimal(5,2)", precision: 5, scale: 2, nullable: true);
        migrationBuilder.AddColumn<string>("TaxItemCode", "QuotationLines", type: "nvarchar(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<string>("TaxTreatmentReason", "QuotationLines", type: "nvarchar(250)", maxLength: 250, nullable: true);
        migrationBuilder.AddColumn<string>("CurrencyCode", "SalesInvoices", type: "nvarchar(3)", maxLength: 3, nullable: true);
        migrationBuilder.AddColumn<string>("SellerProfileJson", "SalesInvoices", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<decimal>("VatAmount", "SalesInvoices", type: "decimal(18,2)", precision: 18, scale: 2, nullable: true);
        migrationBuilder.AddColumn<string>("TaxReportJson", "SalesInvoices", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<string>("TaxReportStatus", "SalesInvoices", type: "nvarchar(32)", maxLength: 32, nullable: true);
        migrationBuilder.AddColumn<string>("TaxAuthorityReference", "SalesInvoices", type: "nvarchar(250)", maxLength: 250, nullable: true);
        migrationBuilder.AddColumn<string>("TaxReportAuditJson", "SalesInvoices", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<decimal>("VatRate", "SalesInvoiceLines", type: "decimal(5,2)", precision: 5, scale: 2, nullable: true);
        migrationBuilder.AddColumn<decimal>("VatAmount", "SalesInvoiceLines", type: "decimal(18,2)", precision: 18, scale: 2, nullable: true);
        migrationBuilder.AddColumn<string>("TaxItemCode", "SalesInvoiceLines", type: "nvarchar(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<string>("TaxTreatmentReason", "SalesInvoiceLines", type: "nvarchar(250)", maxLength: 250, nullable: true);
        migrationBuilder.AddColumn<string>("TaxItemName", "SalesInvoiceLines", type: "nvarchar(400)", maxLength: 400, nullable: true);
        migrationBuilder.AddColumn<string>("TaxUnit", "SalesInvoiceLines", type: "nvarchar(64)", maxLength: 64, nullable: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("CurrencyCode", "Quotations");
        migrationBuilder.DropColumn("VatRate", "QuotationLines");
        migrationBuilder.DropColumn("TaxItemCode", "QuotationLines");
        migrationBuilder.DropColumn("TaxTreatmentReason", "QuotationLines");
        migrationBuilder.DropColumn("CurrencyCode", "SalesInvoices");
        migrationBuilder.DropColumn("SellerProfileJson", "SalesInvoices");
        migrationBuilder.DropColumn("VatAmount", "SalesInvoices");
        migrationBuilder.DropColumn("TaxReportJson", "SalesInvoices");
        migrationBuilder.DropColumn("TaxReportStatus", "SalesInvoices");
        migrationBuilder.DropColumn("TaxAuthorityReference", "SalesInvoices");
        migrationBuilder.DropColumn("TaxReportAuditJson", "SalesInvoices");
        migrationBuilder.DropColumn("VatRate", "SalesInvoiceLines");
        migrationBuilder.DropColumn("VatAmount", "SalesInvoiceLines");
        migrationBuilder.DropColumn("TaxItemCode", "SalesInvoiceLines");
        migrationBuilder.DropColumn("TaxTreatmentReason", "SalesInvoiceLines");
        migrationBuilder.DropColumn("TaxItemName", "SalesInvoiceLines");
        migrationBuilder.DropColumn("TaxUnit", "SalesInvoiceLines");
    }
}
