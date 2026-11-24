using System.Security.Claims;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Production;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public sealed class WarehouseRequestService(IDbContextFactory<ApplicationDbContext> factory, PermissionService permissions)
{
    public const string Requested = "Requested";
    public const string Issued = "Issued";
    public sealed record MaterialLine(string Item, string Unit, decimal Required, decimal Available, string Notes);
    public sealed record RequestRow(long Id, string JobTitle, string JobReference, string Operation, string RequestedBy,
        DateTime RequestedDate, string Status, bool Closed, IReadOnlyList<MaterialLine> Lines)
    {
        public bool CanIssue => Status == Requested && !Closed && Lines.Count > 0 && Lines.All(x => x.Required > 0 && x.Available >= x.Required);
    }

    public async Task<bool> CanViewAsync(ClaimsPrincipal user) =>
        await permissions.HasPermissionAsync(user, "Inventory.Documents.View") &&
        await permissions.HasPermissionAsync(user, "Inventory.Stock.View");

    public async Task<bool> CanIssueAsync(ClaimsPrincipal user) =>
        await CanViewAsync(user) && await permissions.HasPermissionAsync(user, "Inventory.Documents.Post");

    private static bool Closed(ProductionMaterialRequest request) =>
        request.ProductionOrder.Status is ProductionOrderStatus.Cancelled or ProductionOrderStatus.Completed ||
        request.ProductionOperation?.Status is ProductionOperationStatus.Cancelled or ProductionOperationStatus.Completed;

    private static IQueryable<ProductionMaterialRequest> Requests(ApplicationDbContext db) =>
        db.ProductionMaterialRequests.Include(x => x.Lines).ThenInclude(x => x.Item)
            .Include(x => x.Lines).ThenInclude(x => x.Unit)
            .Include(x => x.ProductionOperation)
            .Include(x => x.ProductionOrder).ThenInclude(x => x.SalesOrder).ThenInclude(x => x.Quotation).ThenInclude(x => x.SalesRequest)
            .Include(x => x.ProductionOrder).ThenInclude(x => x.SalesOrder).ThenInclude(x => x.Quotation).ThenInclude(x => x.Lines).ThenInclude(x => x.SourceRequest)
            .AsSplitQuery();

    public async Task<int> PendingCountAsync(ClaimsPrincipal user, CancellationToken token = default)
    {
        if (!await CanViewAsync(user)) return 0;
        await using var db = await factory.CreateDbContextAsync(token);
        return await db.ProductionMaterialRequests.AsNoTracking().CountAsync(x => x.Status == Requested &&
            x.ProductionOrder.Status != ProductionOrderStatus.Cancelled && x.ProductionOrder.Status != ProductionOrderStatus.Completed &&
            (x.ProductionOperation == null || (x.ProductionOperation.Status != ProductionOperationStatus.Completed &&
             x.ProductionOperation.Status != ProductionOperationStatus.Cancelled)), token);
    }

    public async Task<IReadOnlyList<RequestRow>> ReadAsync(ClaimsPrincipal user, bool history)
    {
        if (!await CanViewAsync(user)) throw new UnauthorizedAccessException();
        await using var db = await factory.CreateDbContextAsync();
        var query = Requests(db).AsNoTracking();
        if (history) query = query.Where(x => x.Status != Requested ||
            x.ProductionOrder.Status == ProductionOrderStatus.Completed || x.ProductionOrder.Status == ProductionOrderStatus.Cancelled ||
            (x.ProductionOperation != null && (x.ProductionOperation.Status == ProductionOperationStatus.Completed || x.ProductionOperation.Status == ProductionOperationStatus.Cancelled)));
        else query = query.Where(x => x.Status == Requested &&
            x.ProductionOrder.Status != ProductionOrderStatus.Completed && x.ProductionOrder.Status != ProductionOrderStatus.Cancelled &&
            (x.ProductionOperation == null || (x.ProductionOperation.Status != ProductionOperationStatus.Completed && x.ProductionOperation.Status != ProductionOperationStatus.Cancelled)));
        var requests = await query.OrderBy(x => x.RequestedDate).ThenBy(x => x.Id).ToListAsync();
        var itemIds = requests.SelectMany(x => x.Lines).Select(x => x.ItemId).Distinct().ToArray();
        var orderIds = requests.Select(x => x.ProductionOrderId).Distinct().ToArray();
        var userIds = requests.Select(x => x.RequestedByUserId).OfType<string>().Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FirstName + " " + x.LastName);
        var balances = await db.InventoryBalances.AsNoTracking().UsableMaterials(DateTime.Today)
            .Where(x => x.CustomerId == null && itemIds.Contains(x.ItemId)).ToListAsync();
        var reserved = await db.ProductionMaterialReservations.AsNoTracking().Where(x =>
            orderIds.Contains(x.ProductionOrderId) && itemIds.Contains(x.ItemId) &&
            x.Status == ReservationStatus.Reserved && x.Quantity > 0).ToListAsync();
        return requests.Select(request => new RequestRow(request.Id,
            JobReferences.ProductionTitle(request.ProductionOrder), JobReferences.ProductionLabel(request.ProductionOrder),
            request.ProductionOperation?.Name ?? "—", names.GetValueOrDefault(request.RequestedByUserId ?? "") ?? "—",
            request.RequestedDate, request.Status, Closed(request),
            request.Lines.GroupBy(x => new { x.ItemId, x.UnitId }).Select(g =>
            {
                var first = g.First();
                var available = balances.Where(b => b.ItemId == g.Key.ItemId).Sum(b =>
                    Math.Max(0m, b.OnHandQuantity - b.ReservedQuantity) +
                    Math.Min(Math.Min(b.ReservedQuantity, b.OnHandQuantity), reserved.Where(r =>
                        r.ProductionOrderId == request.ProductionOrderId && r.ItemId == b.ItemId &&
                        r.InventoryLocationId == b.InventoryLocationId && r.InventoryLotId == b.InventoryLotId &&
                        r.UnitId == g.Key.UnitId).Sum(r => r.Quantity)));
                if (!first.Item.IsActive || first.Item.UnitId != g.Key.UnitId) available = 0;
                return new MaterialLine(first.Item.Name, first.Unit.Symbol, g.Sum(x => x.Quantity),
                    available, string.Join(" · ", g.Select(x => x.Notes).Where(n => !string.IsNullOrWhiteSpace(n))));
            }).ToList())).ToList();
    }

    // Full fulfilment is atomic: either every requested material moves, or none does.
    // The existing production issue posting path records the same reservation and
    // issue accounting semantics; no separate stock ledger is introduced.
    public async Task<IReadOnlyList<string>> IssueAsync(long requestId, ClaimsPrincipal user)
    {
        if (!await CanIssueAsync(user)) throw new UnauthorizedAccessException("Inventory posting permission required.");
        var employee = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Employee identity required.");
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (!await db.Users.AnyAsync(x => x.Id == employee && x.IsActive))
            throw new UnauthorizedAccessException("Active employee account required.");
        var request = await Requests(db).FirstOrDefaultAsync(x => x.Id == requestId)
            ?? throw new InvalidOperationException("Material request not found.");
        if (request.Status != Requested || Closed(request))
            throw new InvalidOperationException("This request is no longer open.");
        if (request.Lines.Count == 0 || request.Lines.Any(x => x.Quantity <= 0 || !x.Item.IsActive || x.UnitId != x.Item.UnitId))
            throw new InvalidOperationException("Invalid material request lines.");
        var destination = await db.InventoryLocations.FirstOrDefaultAsync(x => x.Code == "PROD01" &&
            x.IsActive && x.Type == InventoryLocationType.Production)
            ?? throw new InvalidOperationException("Configure the active PROD01 production floor location.");
        var itemIds = request.Lines.Select(x => x.ItemId).Distinct().ToArray();
        var balances = await db.InventoryBalances.UsableMaterials(DateTime.Today)
            .Include(x => x.InventoryLot)
            .Where(x => x.CustomerId == null && itemIds.Contains(x.ItemId) && x.OnHandQuantity > 0)
            .OrderBy(x => x.InventoryLot == null || !x.InventoryLot.ExpirationDate.HasValue)
            .ThenBy(x => x.InventoryLot!.ExpirationDate).ThenBy(x => x.InventoryLotId)
            .ThenBy(x => x.InventoryLocationId).ThenBy(x => x.Id).ToListAsync();
        var reservations = await db.ProductionMaterialReservations.Where(x =>
            x.ProductionOrderId == request.ProductionOrderId && x.Status == ReservationStatus.Reserved &&
            itemIds.Contains(x.ItemId) && x.Quantity > 0).OrderBy(x => x.Id).ToListAsync();
        var allocation = new List<(InventoryBalance Balance, int UnitId, decimal Quantity)>();
        foreach (var requirement in request.Lines.GroupBy(x => new { x.ItemId, x.UnitId }))
        {
            var remaining = requirement.Sum(x => x.Quantity);
            var eligible = balances.Where(b => b.ItemId == requirement.Key.ItemId).ToList();
            // First issue stock already reserved for this job, then unreserved stock.
            foreach (var balance in eligible)
            {
                var own = reservations.Where(r => r.ItemId == balance.ItemId &&
                    r.InventoryLocationId == balance.InventoryLocationId && r.InventoryLotId == balance.InventoryLotId &&
                    r.UnitId == requirement.Key.UnitId).ToList();
                foreach (var reservation in own)
                {
                    if (remaining <= 0) break;
                    var used = Math.Min(remaining, reservation.Quantity);
                    if (balance.ReservedQuantity < used || balance.OnHandQuantity < balance.ReservedQuantity)
                        throw new InvalidOperationException("Reserved stock is inconsistent. Review the inventory balance.");
                    reservation.Quantity -= used;
                    if (reservation.Quantity == 0) reservation.Status = ReservationStatus.Consumed;
                    balance.ReservedQuantity -= used;
                    allocation.Add((balance, requirement.Key.UnitId, used));
                    remaining -= used;
                }
            }
            foreach (var balance in eligible)
            {
                if (remaining <= 0) break;
                var alreadyAllocated = allocation.Where(x => x.Balance.Id == balance.Id).Sum(x => x.Quantity);
                var free = Math.Max(0, balance.OnHandQuantity - balance.ReservedQuantity - alreadyAllocated);
                var used = Math.Min(remaining, free);
                if (used <= 0) continue;
                allocation.Add((balance, requirement.Key.UnitId, used));
                remaining -= used;
            }
            if (remaining > 0)
                throw new InvalidOperationException($"Insufficient usable warehouse stock for {requirement.First().Item.Name}. Short by {remaining:N3}.");
        }
        var posting = new InventoryPostingService(db);
        var numbering = new NumberSeriesService(db);
        var numbers = new List<string>();
        foreach (var source in allocation.GroupBy(x => x.Balance.InventoryLocationId))
        {
            var number = await numbering.GetNextNumberAsync(NumberSeriesType.ProductionIssue, useTransaction: false);
            var reference = $"Material request #{request.Id}; {JobReferences.ProductionLabel(request.ProductionOrder)}; {request.ProductionOrder.OrderNumber}";
            var document = new InventoryDocument
            {
                DocumentNumber = number, DocumentType = InventoryDocumentType.ProductionIssue,
                DocumentDate = DateTime.Now, FromLocationId = source.Key, ToLocationId = destination.Id,
                ProductionOrderId = request.ProductionOrderId, Status = InventoryDocumentStatus.Draft,
                UserId = employee, Notes = reference
            };
            foreach (var group in source.GroupBy(x => new { x.Balance.ItemId, x.Balance.InventoryLotId, x.UnitId }))
            {
                var first = group.First().Balance;
                document.Lines.Add(new InventoryDocumentLine
                {
                    ItemId = group.Key.ItemId, UnitId = group.Key.UnitId, Quantity = group.Sum(x => x.Quantity),
                    InventoryLotId = group.Key.InventoryLotId, LotNumber = first.InventoryLot?.LotNumber,
                    Notes = reference
                });
            }
            db.InventoryDocuments.Add(document);
            await db.SaveChangesAsync();
            await posting.PostAsync(document.Id, employee, useTransaction: false);
            foreach (var line in document.Lines)
                db.ProductionMaterialConsumptions.Add(new ProductionMaterialConsumption
                {
                    ProductionOrderId = request.ProductionOrderId, ItemId = line.ItemId, UnitId = line.UnitId,
                    Quantity = line.Quantity, ConsumptionDate = DateTime.Now, InventoryDocumentId = document.Id
                });
            numbers.Add(number);
        }
        request.Status = Issued;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return numbers;
    }
}
