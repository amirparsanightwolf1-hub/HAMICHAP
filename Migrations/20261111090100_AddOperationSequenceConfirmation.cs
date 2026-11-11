using System;
using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HamiChap_MES_ERP.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261111090100_AddOperationSequenceConfirmation")]
public partial class AddOperationSequenceConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "OperationsRouteInitialized",
            table: "ProductionOrders",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "OperationsSequenceConfirmedAt",
            table: "ProductionOrders",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OperationsSequenceConfirmedBy",
            table: "ProductionOrders",
            type: "nvarchar(450)",
            maxLength: 450,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OperationsSequenceSignature",
            table: "ProductionOrders",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "OperationsRouteInitialized", table: "ProductionOrders");
        migrationBuilder.DropColumn(name: "OperationsSequenceConfirmedAt", table: "ProductionOrders");
        migrationBuilder.DropColumn(name: "OperationsSequenceConfirmedBy", table: "ProductionOrders");
        migrationBuilder.DropColumn(name: "OperationsSequenceSignature", table: "ProductionOrders");
    }
}
