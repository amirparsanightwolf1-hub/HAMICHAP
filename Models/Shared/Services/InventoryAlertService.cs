using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class InventoryAlertService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    // Single calculation for the stock page, inbox and current shortages.
    // Company-owned usable storage stock only; zero-balance items are included.
    internal static async Task<List<LowStockAlert>> ReadMonitoredAsync(ApplicationDbContext db,
        CancellationToken cancellationToken = default, int? itemId = null)
    {
        var items = db.Items.AsNoTracking().Where(x => x.IsActive && x.EnableLowStockAlert);
        if (itemId.HasValue) items = items.Where(x => x.Id == itemId.Value);
        var day = DateTime.Today;
        var quantities = db.InventoryBalances.AsNoTracking().UsableStock(day)
            .Where(x => x.CustomerId == null && x.Item.EnableLowStockAlert)
            .GroupBy(x => x.ItemId)
            .Select(g => new { ItemId = g.Key, Available = g.Sum(x => x.OnHandQuantity - x.ReservedQuantity) });
        // Left join preserves items that have no balance rows yet.
        return await (from item in items
                      join quantity in quantities on item.Id equals quantity.ItemId into available
                      from quantity in available.DefaultIfEmpty()
                      select new LowStockAlert
                      {
                          ItemId = item.Id, ItemCode = item.Code, ItemName = item.Name,
                          UnitSymbol = item.Unit.Symbol,
                          AvailableQuantity = quantity == null ? 0m : quantity.Available,
                          Threshold = item.LowStockThreshold
                      }).ToListAsync(cancellationToken);
    }

    public async Task<List<LowStockAlert>> GetMonitoredStockAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await ReadMonitoredAsync(db, cancellationToken);
    }

    public async Task<List<LowStockAlert>> GetLowStockAlertsAsync(CancellationToken cancellationToken = default) =>
        (await GetMonitoredStockAsync(cancellationToken))
            .Where(x => x.AvailableQuantity <= x.Threshold)
            .OrderBy(x => x.AvailableQuantity > 0)
            .ThenBy(x => x.ItemName).ThenBy(x => x.ItemId).ToList();

    public async Task<bool> IsLowStockAsync(int itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await ReadMonitoredAsync(db, cancellationToken, itemId);
        return rows.Any(x => x.AvailableQuantity <= x.Threshold);
    }

    public async Task<decimal> GetCompanyAvailableQuantityAsync(int itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.InventoryBalances.UsableStock(DateTime.Today)
            .Where(x => x.ItemId == itemId && x.CustomerId == null)
            .SumAsync(x => x.OnHandQuantity - x.ReservedQuantity, cancellationToken);
    }
}
