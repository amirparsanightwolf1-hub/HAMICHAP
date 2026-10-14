using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HamiChap_MES_ERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesDeliveryAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FromCustomerId",
                table: "InventoryDocumentLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToCustomerId",
                table: "InventoryDocumentLines",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SalesDeliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeliveryNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SalesOrderId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromLocationId = table.Column<int>(type: "int", nullable: false),
                    ToLocationId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesDeliveries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SalesDeliveries_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesDeliveries_InventoryLocations_FromLocationId",
                        column: x => x.FromLocationId,
                        principalTable: "InventoryLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesDeliveries_InventoryLocations_ToLocationId",
                        column: x => x.ToLocationId,
                        principalTable: "InventoryLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesDeliveries_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesDeliveryLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesDeliveryId = table.Column<long>(type: "bigint", nullable: false),
                    SalesOrderLineId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    InventoryDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesDeliveryLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesDeliveryLines_InventoryDocuments_InventoryDocumentId",
                        column: x => x.InventoryDocumentId,
                        principalTable: "InventoryDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesDeliveryLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesDeliveryLines_SalesDeliveries_SalesDeliveryId",
                        column: x => x.SalesDeliveryId,
                        principalTable: "SalesDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesDeliveryLines_SalesOrderLines_SalesOrderLineId",
                        column: x => x.SalesOrderLineId,
                        principalTable: "SalesOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesDeliveryLines_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesDeliveryAllocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesDeliveryLineId = table.Column<long>(type: "bigint", nullable: false),
                    InventoryLotId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesDeliveryAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesDeliveryAllocations_InventoryLots_InventoryLotId",
                        column: x => x.InventoryLotId,
                        principalTable: "InventoryLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesDeliveryAllocations_SalesDeliveryLines_SalesDeliveryLineId",
                        column: x => x.SalesDeliveryLineId,
                        principalTable: "SalesDeliveryLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentLines_FromCustomerId",
                table: "InventoryDocumentLines",
                column: "FromCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentLines_ToCustomerId",
                table: "InventoryDocumentLines",
                column: "ToCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveries_CustomerId",
                table: "SalesDeliveries",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveries_FromLocationId",
                table: "SalesDeliveries",
                column: "FromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveries_SalesOrderId",
                table: "SalesDeliveries",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveries_ToLocationId",
                table: "SalesDeliveries",
                column: "ToLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveries_UserId",
                table: "SalesDeliveries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveryAllocations_InventoryLotId",
                table: "SalesDeliveryAllocations",
                column: "InventoryLotId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveryAllocations_SalesDeliveryLineId",
                table: "SalesDeliveryAllocations",
                column: "SalesDeliveryLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveryLines_InventoryDocumentId",
                table: "SalesDeliveryLines",
                column: "InventoryDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveryLines_ItemId",
                table: "SalesDeliveryLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveryLines_SalesDeliveryId",
                table: "SalesDeliveryLines",
                column: "SalesDeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveryLines_SalesOrderLineId",
                table: "SalesDeliveryLines",
                column: "SalesOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveryLines_UnitId",
                table: "SalesDeliveryLines",
                column: "UnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryDocumentLines_Customers_FromCustomerId",
                table: "InventoryDocumentLines",
                column: "FromCustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryDocumentLines_Customers_ToCustomerId",
                table: "InventoryDocumentLines",
                column: "ToCustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryDocumentLines_Customers_FromCustomerId",
                table: "InventoryDocumentLines");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryDocumentLines_Customers_ToCustomerId",
                table: "InventoryDocumentLines");

            migrationBuilder.DropTable(
                name: "SalesDeliveryAllocations");

            migrationBuilder.DropTable(
                name: "SalesDeliveryLines");

            migrationBuilder.DropTable(
                name: "SalesDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_InventoryDocumentLines_FromCustomerId",
                table: "InventoryDocumentLines");

            migrationBuilder.DropIndex(
                name: "IX_InventoryDocumentLines_ToCustomerId",
                table: "InventoryDocumentLines");

            migrationBuilder.DropColumn(
                name: "FromCustomerId",
                table: "InventoryDocumentLines");

            migrationBuilder.DropColumn(
                name: "ToCustomerId",
                table: "InventoryDocumentLines");
        }
    }
}
