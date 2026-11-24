using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HamiChap_MES_ERP.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261124140900_AddCustomerProfiles")]
public partial class AddCustomerProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "CustomerType", table: "Customers", type: "int", nullable: false, defaultValue: 0);
        foreach (var column in new[] { "NationalId", "NationalCode", "EconomicCode", "RegistrationNumber", "Mobile", "PostalCode", "Province", "City", "ContactPerson" })
            migrationBuilder.AddColumn<string>(name: column, table: "Customers", type: "nvarchar(max)", nullable: false, defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CustomerType", table: "Customers");
        foreach (var column in new[] { "NationalId", "NationalCode", "EconomicCode", "RegistrationNumber", "Mobile", "PostalCode", "Province", "City", "ContactPerson" })
            migrationBuilder.DropColumn(name: column, table: "Customers");
    }
}
