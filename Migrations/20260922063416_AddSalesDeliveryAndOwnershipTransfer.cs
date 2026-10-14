using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HamiChap_MES_ERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesDeliveryAndOwnershipTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DeliveredQuantity",
                table: "SalesOrderLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProducedQuantity",
                table: "SalesOrderLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "ProductionOrderId",
                table: "InventoryDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "QualityInspectionId",
                table: "InventoryDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MachineCalendars",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsWorkingDay = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineCalendars_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionSchedules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionOperationId = table.Column<long>(type: "bigint", nullable: false),
                    MachineId = table.Column<int>(type: "int", nullable: false),
                    PlannedStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlannedEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionSchedules_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionSchedules_ProductionOperations_ProductionOperationId",
                        column: x => x.ProductionOperationId,
                        principalTable: "ProductionOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QualityInspections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    InspectedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AcceptedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RejectedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReworkQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InspectorUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InspectionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityInspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityInspections_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityInspectionResults",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QualityInspectionId = table.Column<long>(type: "bigint", nullable: false),
                    ParameterName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MeasuredValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TextValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MinimumValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MaximumValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Passed = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityInspectionResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityInspectionResults_QualityInspections_QualityInspectionId",
                        column: x => x.QualityInspectionId,
                        principalTable: "QualityInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_ProductionOrderId",
                table: "InventoryDocuments",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_QualityInspectionId",
                table: "InventoryDocuments",
                column: "QualityInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineCalendars_MachineId",
                table: "MachineCalendars",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSchedules_MachineId",
                table: "ProductionSchedules",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSchedules_ProductionOperationId",
                table: "ProductionSchedules",
                column: "ProductionOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityInspectionResults_QualityInspectionId",
                table: "QualityInspectionResults",
                column: "QualityInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityInspections_ProductionOrderId",
                table: "QualityInspections",
                column: "ProductionOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryDocuments_ProductionOrders_ProductionOrderId",
                table: "InventoryDocuments",
                column: "ProductionOrderId",
                principalTable: "ProductionOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryDocuments_QualityInspections_QualityInspectionId",
                table: "InventoryDocuments",
                column: "QualityInspectionId",
                principalTable: "QualityInspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryDocuments_ProductionOrders_ProductionOrderId",
                table: "InventoryDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryDocuments_QualityInspections_QualityInspectionId",
                table: "InventoryDocuments");

            migrationBuilder.DropTable(
                name: "MachineCalendars");

            migrationBuilder.DropTable(
                name: "ProductionSchedules");

            migrationBuilder.DropTable(
                name: "QualityInspectionResults");

            migrationBuilder.DropTable(
                name: "QualityInspections");

            migrationBuilder.DropIndex(
                name: "IX_InventoryDocuments_ProductionOrderId",
                table: "InventoryDocuments");

            migrationBuilder.DropIndex(
                name: "IX_InventoryDocuments_QualityInspectionId",
                table: "InventoryDocuments");

            migrationBuilder.DropColumn(
                name: "DeliveredQuantity",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "ProducedQuantity",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "ProductionOrderId",
                table: "InventoryDocuments");

            migrationBuilder.DropColumn(
                name: "QualityInspectionId",
                table: "InventoryDocuments");
        }
    }
}
