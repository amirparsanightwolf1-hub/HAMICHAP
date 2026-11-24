using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace HamiChap_MES_ERP.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261124213000_AddEmployeeInbox")]
public partial class AddEmployeeInbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "EmployeeInboxItems", columns: table => new
        {
            Id = table.Column<long>(type:"bigint", nullable:false).Annotation("SqlServer:Identity","1, 1"),
            RecipientUserId = table.Column<string>(type:"nvarchar(450)",maxLength:450,nullable:false),
            SenderUserId = table.Column<string>(type:"nvarchar(450)",maxLength:450,nullable:true),
            EventKey = table.Column<string>(type:"nvarchar(120)",maxLength:120,nullable:false),
            Kind = table.Column<string>(type:"nvarchar(32)",maxLength:32,nullable:false),
            SourceId = table.Column<long>(type:"bigint",nullable:true),
            Subject = table.Column<string>(type:"nvarchar(400)",maxLength:400,nullable:false),
            SubjectFa = table.Column<string>(type:"nvarchar(400)",maxLength:400,nullable:true),
            Body = table.Column<string>(type:"nvarchar(max)",nullable:false),
            BodyFa = table.Column<string>(type:"nvarchar(max)",nullable:true),
            CreatedAtUtc = table.Column<DateTime>(type:"datetime2",nullable:false),
            ReadAtUtc = table.Column<DateTime>(type:"datetime2",nullable:true),
            RowVersion = table.Column<byte[]>(type:"rowversion",rowVersion:true,nullable:false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_EmployeeInboxItems", x => x.Id);
            table.ForeignKey("FK_EmployeeInboxItems_AspNetUsers_RecipientUserId", x => x.RecipientUserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_EmployeeInboxItems_RecipientUserId_EventKey", "EmployeeInboxItems", new[] { "RecipientUserId", "EventKey" }, unique:true);
        migrationBuilder.CreateIndex("IX_EmployeeInboxItems_RecipientUserId_ReadAtUtc_CreatedAtUtc", "EmployeeInboxItems", new[] { "RecipientUserId", "ReadAtUtc", "CreatedAtUtc" });
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("EmployeeInboxItems");
}
