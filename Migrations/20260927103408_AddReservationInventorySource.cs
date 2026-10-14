using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HamiChap_MES_ERP.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationInventorySource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InventoryLocationId",
                table: "ProductionMaterialReservations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "InventoryLotId",
                table: "ProductionMaterialReservations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialReservations_InventoryLocationId",
                table: "ProductionMaterialReservations",
                column: "InventoryLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialReservations_InventoryLotId",
                table: "ProductionMaterialReservations",
                column: "InventoryLotId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionMaterialReservations_InventoryLocations_InventoryLocationId",
                table: "ProductionMaterialReservations",
                column: "InventoryLocationId",
                principalTable: "InventoryLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionMaterialReservations_InventoryLots_InventoryLotId",
                table: "ProductionMaterialReservations",
                column: "InventoryLotId",
                principalTable: "InventoryLots",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionMaterialReservations_InventoryLocations_InventoryLocationId",
                table: "ProductionMaterialReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionMaterialReservations_InventoryLots_InventoryLotId",
                table: "ProductionMaterialReservations");

            migrationBuilder.DropIndex(
                name: "IX_ProductionMaterialReservations_InventoryLocationId",
                table: "ProductionMaterialReservations");

            migrationBuilder.DropIndex(
                name: "IX_ProductionMaterialReservations_InventoryLotId",
                table: "ProductionMaterialReservations");

            migrationBuilder.DropColumn(
                name: "InventoryLocationId",
                table: "ProductionMaterialReservations");

            migrationBuilder.DropColumn(
                name: "InventoryLotId",
                table: "ProductionMaterialReservations");
        }
    }
}
