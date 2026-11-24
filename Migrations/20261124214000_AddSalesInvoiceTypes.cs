using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace HamiChap_MES_ERP.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261124214000_AddSalesInvoiceTypes")]
public partial class AddSalesInvoiceTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("InvoiceType", "SalesInvoices", type: "int", nullable: true);
        migrationBuilder.AddColumn<string>("BuyerProfileJson", "SalesInvoices", type: "nvarchar(max)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("InvoiceType", "SalesInvoices");
        migrationBuilder.DropColumn("BuyerProfileJson", "SalesInvoices");
    }
}
