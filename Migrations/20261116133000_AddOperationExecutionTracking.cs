using System;
using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HamiChap_MES_ERP.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261116133000_AddOperationExecutionTracking")]
public partial class AddOperationExecutionTracking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProductionOperationEvents",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ProductionOperationId = table.Column<long>(type: "bigint", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                OperatorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductionOperationEvents", x => x.Id);
                table.ForeignKey("FK_ProductionOperationEvents_ProductionOperations_ProductionOperationId",
                    x => x.ProductionOperationId, "ProductionOperations", "Id",
                    onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(
            name: "IX_ProductionOperationEvents_ProductionOperationId_OccurredAtUtc",
            table: "ProductionOperationEvents",
            columns: new[] { "ProductionOperationId", "OccurredAtUtc" });

        migrationBuilder.AddColumn<string>(
            name: "RequestedByUserId", table: "ProductionMaterialRequests",
            type: "nvarchar(450)", maxLength: 450, nullable: true);
        migrationBuilder.AddColumn<long>(
            name: "ProductionOperationId", table: "ProductionMaterialRequests",
            type: "bigint", nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_ProductionMaterialRequests_ProductionOperationId",
            table: "ProductionMaterialRequests", column: "ProductionOperationId");
        migrationBuilder.AddForeignKey(
            name: "FK_ProductionMaterialRequests_ProductionOperations_ProductionOperationId",
            table: "ProductionMaterialRequests", column: "ProductionOperationId",
            principalTable: "ProductionOperations", principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ProductionMaterialRequests_ProductionOperations_ProductionOperationId",
            table: "ProductionMaterialRequests");
        migrationBuilder.DropIndex(name: "IX_ProductionMaterialRequests_ProductionOperationId",
            table: "ProductionMaterialRequests");
        migrationBuilder.DropColumn(name: "ProductionOperationId", table: "ProductionMaterialRequests");
        migrationBuilder.DropColumn(name: "RequestedByUserId", table: "ProductionMaterialRequests");
        migrationBuilder.DropTable(name: "ProductionOperationEvents");
    }
}
