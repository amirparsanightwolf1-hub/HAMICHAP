using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace HamiChap_MES_ERP.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261124210000_AddMaterialSettlement")]
public partial class AddMaterialSettlement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>("ActualConsumedQuantity", "InventoryDocumentLines", type:"decimal(18,3)", precision:18, scale:3, nullable:false, defaultValue:0m);
        migrationBuilder.AddColumn<decimal>("ReturnedQuantity", "InventoryDocumentLines", type:"decimal(18,3)", precision:18, scale:3, nullable:false, defaultValue:0m);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("ActualConsumedQuantity", "InventoryDocumentLines");
        migrationBuilder.DropColumn("ReturnedQuantity", "InventoryDocumentLines");
    }
}
