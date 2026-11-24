using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace HamiChap_MES_ERP.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261124191500_AddSalesProofRevisions")]
public partial class AddSalesProofRevisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SalesProofRevisions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                SalesRequestId = table.Column<long>(type: "bigint", nullable: false),
                RevisionNumber = table.Column<int>(type: "int", nullable: false),
                SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                SentByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                DecisionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                DecisionByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                DecisionNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SalesProofRevisions", x => x.Id);
                table.ForeignKey("FK_SalesProofRevisions_SalesRequests_SalesRequestId", x => x.SalesRequestId, "SalesRequests", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_SalesProofRevisions_SalesRequestId_RevisionNumber", "SalesProofRevisions", new[] { "SalesRequestId", "RevisionNumber" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("SalesProofRevisions");
}
