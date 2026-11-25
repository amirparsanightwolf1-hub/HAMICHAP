using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace HamiChap_MES_ERP.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261125090000_AddQuotationDocumentTypes")]
public partial class AddQuotationDocumentTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("QuotationType", "Quotations", type: "int", nullable: true);
        migrationBuilder.AddColumn<string>("DocumentPartiesJson", "Quotations", type: "nvarchar(max)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("QuotationType", "Quotations");
        migrationBuilder.DropColumn("DocumentPartiesJson", "Quotations");
    }
}
