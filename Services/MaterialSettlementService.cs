using System.Security.Claims;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.EntityFrameworkCore;
namespace HamiChap_MES_ERP.Services;

public sealed class MaterialSettlementService(IDbContextFactory<ApplicationDbContext> factory, WarehouseRequestService access)
{
    public sealed record IssueRow(long Id, string Job, string Number, string Item, string Lot, string Unit,
        decimal Issued, decimal Consumed, decimal Returned)
    {
        public decimal Remaining => Issued - Consumed - Returned;
    }
    private static IQueryable<InventoryDocumentLine> Issues(ApplicationDbContext db) =>
        db.InventoryDocumentLines.Where(x => x.InventoryDocument.Status == InventoryDocumentStatus.Posted &&
            x.InventoryDocument.DocumentType == InventoryDocumentType.ProductionIssue &&
            x.InventoryDocument.ProductionOrderId.HasValue && x.CustomerId == null &&
            x.FromCustomerId == null && x.ToCustomerId == null &&
            x.InventoryDocument.FromLocation != null && x.InventoryDocument.FromLocation.Type == InventoryLocationType.Warehouse &&
            x.InventoryDocument.ToLocation != null &&
            (x.InventoryDocument.ToLocation.Type == InventoryLocationType.Production || x.InventoryDocument.ToLocation.Type == InventoryLocationType.Machine));

    public async Task<List<IssueRow>> ReadAsync(ClaimsPrincipal user)
    {
        if (!await access.CanViewAsync(user)) throw new UnauthorizedAccessException();
        await using var db = await factory.CreateDbContextAsync();
        var rows = await Issues(db).AsNoTracking().Where(x => x.Quantity > x.ActualConsumedQuantity + x.ReturnedQuantity)
            .Include(x => x.Item).Include(x => x.Unit).Include(x => x.InventoryLot)
            .Include(x => x.InventoryDocument).ThenInclude(x => x.ProductionOrder).ThenInclude(x => x!.SalesOrder).ThenInclude(x => x.Quotation).ThenInclude(x => x.SalesRequest)
            .Include(x => x.InventoryDocument).ThenInclude(x => x.ProductionOrder).ThenInclude(x => x!.SalesOrder).ThenInclude(x => x.Quotation).ThenInclude(x => x.Lines).ThenInclude(x => x.SourceRequest)
            .AsSplitQuery().OrderByDescending(x => x.InventoryDocument.DocumentDate).ToListAsync();
        return rows.Select(x => new IssueRow(x.Id, JobReferences.ProductionTitle(x.InventoryDocument.ProductionOrder),
            x.InventoryDocument.DocumentNumber, x.Item.Name, x.InventoryLot?.LotNumber ?? "—", x.Unit.Symbol,
            x.Quantity, x.ActualConsumedQuantity, x.ReturnedQuantity)).ToList();
    }
    public async Task SettleAsync(long lineId, decimal used, decimal returned, ClaimsPrincipal user)
    {
        if (!await access.CanIssueAsync(user)) throw new UnauthorizedAccessException("Warehouse posting permission required.");
        var employee = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        if (used < 0 || returned < 0 || used + returned <= 0 || decimal.Round(used, 3) != used || decimal.Round(returned, 3) != returned)
            throw new InvalidOperationException("Enter positive quantities with up to three decimal places.");
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (!await db.Users.AnyAsync(x => x.Id == employee && x.IsActive)) throw new UnauthorizedAccessException();
        var line = await Issues(db).Include(x => x.InventoryDocument).Include(x => x.Item).Include(x => x.InventoryLot)
            .FirstOrDefaultAsync(x => x.Id == lineId) ?? throw new InvalidOperationException("Original issue not found.");
        if (used + returned > line.Quantity - line.ActualConsumedQuantity - line.ReturnedQuantity)
            throw new InvalidOperationException("Usage plus return exceeds remaining issued material.");
        var issue = line.InventoryDocument;
        var available = await db.InventoryBalances.Where(x => x.ItemId == line.ItemId && x.InventoryLotId == line.InventoryLotId &&
            x.CustomerId == null && x.InventoryLocationId == issue.ToLocationId).SumAsync(x => x.OnHandQuantity - x.ReservedQuantity);
        if (available < used + returned) throw new InvalidOperationException("Insufficient production-floor balance. Reconcile existing movements first.");
        var poster = new InventoryPostingService(db);
        var numbering = new NumberSeriesService(db);
        async Task PostAsync(InventoryDocumentType type, int destination, decimal quantity)
        {
            if (quantity <= 0) return;
            var document = new InventoryDocument
            {
                DocumentNumber = await numbering.GetNextNumberAsync(InventoryDocumentNumbering.SeriesFor(type), false),
                DocumentType = type, DocumentDate = DateTime.Now, FromLocationId = issue.ToLocationId, ToLocationId = destination,
                ProductionOrderId = issue.ProductionOrderId, Status = InventoryDocumentStatus.Draft, UserId = employee,
                Notes = $"Settlement of {issue.DocumentNumber}, issue line {line.Id}"
            };
            document.Lines.Add(new InventoryDocumentLine { ItemId = line.ItemId, UnitId = line.UnitId, Quantity = quantity,
                InventoryLotId = line.InventoryLotId, LotNumber = line.InventoryLot?.LotNumber, Notes = document.Notes });
            db.InventoryDocuments.Add(document);
            await db.SaveChangesAsync();
            await poster.PostAsync(document.Id, employee, useTransaction:false, allowProductionConsumption:true);
        }
        if (used > 0)
        {
            var sink = await db.InventoryLocations.FirstOrDefaultAsync(x => x.Code == "CONSUMED");
            if (sink == null)
            {
                sink = new InventoryLocation { Code = "CONSUMED", Name = "Consumed materials (ledger only)", Type = InventoryLocationType.External, IsActive = true };
                db.InventoryLocations.Add(sink);
                await db.SaveChangesAsync();
            }
            await PostAsync(InventoryDocumentType.ProductionConsumption, sink.Id, used);
        }
        if (returned > 0) await PostAsync(InventoryDocumentType.MaterialTransfer, issue.FromLocationId!.Value, returned);
        line.ActualConsumedQuantity += used;
        line.ReturnedQuantity += returned;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
