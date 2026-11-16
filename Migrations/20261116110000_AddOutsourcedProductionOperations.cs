using System;
using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HamiChap_MES_ERP.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261116110000_AddOutsourcedProductionOperations")]
public partial class AddOutsourcedProductionOperations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsOutsourced",
            table: "ProductionOperations",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "OutsourcedPlannedStart",
            table: "ProductionOperations",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "OutsourcedPlannedEnd",
            table: "ProductionOperations",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsOutsourced", table: "ProductionOperations");
        migrationBuilder.DropColumn(name: "OutsourcedPlannedStart", table: "ProductionOperations");
        migrationBuilder.DropColumn(name: "OutsourcedPlannedEnd", table: "ProductionOperations");
    }
}
