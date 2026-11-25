using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace HamiChap_MES_ERP.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261125100000_AddSellerCompanies")]
public partial class AddSellerCompanies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SellerCompanies",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                LegalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                NationalId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                EconomicCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                RegistrationNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Address = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                PostalCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Phone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                Jurisdiction = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                TaxMemoryId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SellerCompanies", x => x.Id));
        migrationBuilder.CreateIndex("IX_SellerCompanies_LegalName", "SellerCompanies", "LegalName");
        migrationBuilder.CreateIndex("IX_SellerCompanies_Jurisdiction_NationalId", "SellerCompanies", new[] { "Jurisdiction", "NationalId" }, unique: true);
        migrationBuilder.AddColumn<int>("SellerCompanyId", "Quotations", type: "int", nullable: true);
        migrationBuilder.CreateIndex("IX_Quotations_SellerCompanyId", "Quotations", "SellerCompanyId");
        migrationBuilder.AddForeignKey("FK_Quotations_SellerCompanies_SellerCompanyId", "Quotations", "SellerCompanyId", "SellerCompanies", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_Quotations_SellerCompanies_SellerCompanyId", "Quotations");
        migrationBuilder.DropIndex("IX_Quotations_SellerCompanyId", "Quotations");
        migrationBuilder.DropColumn("SellerCompanyId", "Quotations");
        migrationBuilder.DropTable("SellerCompanies");
    }
}
