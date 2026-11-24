using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace HamiChap_MES_ERP.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261124211000_AddInvoicePaymentConfirmation")]
public partial class AddInvoicePaymentConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>("PaidAtUtc", "SalesInvoices", type:"datetime2", nullable:true);
        migrationBuilder.AddColumn<string>("PaidByUserId", "SalesInvoices", type:"nvarchar(450)", maxLength:450, nullable:true);
        migrationBuilder.AddColumn<string>("PaymentReference", "SalesInvoices", type:"nvarchar(500)", maxLength:500, nullable:true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("PaidAtUtc","SalesInvoices");
        migrationBuilder.DropColumn("PaidByUserId","SalesInvoices");
        migrationBuilder.DropColumn("PaymentReference","SalesInvoices");
    }
}
