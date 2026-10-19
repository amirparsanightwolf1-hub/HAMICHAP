using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HamiChap_MES_ERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesInvoiceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "QuotationLineId",
                table: "SalesOrderLines",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SalesInvoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    QuotationId = table.Column<long>(type: "bigint", nullable: false),
                    SalesOrderId = table.Column<long>(type: "bigint", nullable: false),
                    SalesDeliveryId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesInvoices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesInvoices_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesInvoices_SalesDeliveries_SalesDeliveryId",
                        column: x => x.SalesDeliveryId,
                        principalTable: "SalesDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesInvoices_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesInvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    SalesDeliveryLineId = table.Column<long>(type: "bigint", nullable: false),
                    QuotationLineId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesInvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceLines_QuotationLines_QuotationLineId",
                        column: x => x.QuotationLineId,
                        principalTable: "QuotationLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceLines_SalesDeliveryLines_SalesDeliveryLineId",
                        column: x => x.SalesDeliveryLineId,
                        principalTable: "SalesDeliveryLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceLines_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceLines_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLines_QuotationLineId",
                table: "SalesOrderLines",
                column: "QuotationLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceLines_ItemId",
                table: "SalesInvoiceLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceLines_QuotationLineId",
                table: "SalesInvoiceLines",
                column: "QuotationLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceLines_SalesDeliveryLineId",
                table: "SalesInvoiceLines",
                column: "SalesDeliveryLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceLines_SalesInvoiceId",
                table: "SalesInvoiceLines",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceLines_UnitId",
                table: "SalesInvoiceLines",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_CustomerId",
                table: "SalesInvoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_InvoiceNumber",
                table: "SalesInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_QuotationId",
                table: "SalesInvoices",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_SalesDeliveryId",
                table: "SalesInvoices",
                column: "SalesDeliveryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_SalesOrderId",
                table: "SalesInvoices",
                column: "SalesOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrderLines_QuotationLines_QuotationLineId",
                table: "SalesOrderLines",
                column: "QuotationLineId",
                principalTable: "QuotationLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrderLines_QuotationLines_QuotationLineId",
                table: "SalesOrderLines");

            migrationBuilder.DropTable(
                name: "SalesInvoiceLines");

            migrationBuilder.DropTable(
                name: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrderLines_QuotationLineId",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "QuotationLineId",
                table: "SalesOrderLines");
        }
    }
}
