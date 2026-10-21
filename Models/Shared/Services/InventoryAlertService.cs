using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class InventoryAlertService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public InventoryAlertService(
        IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<LowStockAlert>> GetLowStockAlertsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(cancellationToken);

        var items = await db.Items
            .AsNoTracking()
            .Include(x => x.Unit)
            .Where(x => x.IsActive)
            .Where(x => x.EnableLowStockAlert)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return new List<LowStockAlert>();

        var itemIds = items
            .Select(x => x.Id)
            .ToList();

        var balances = await db.InventoryBalances
            .AsNoTracking()
            .Where(x =>
                x.CustomerId == null &&
                itemIds.Contains(x.ItemId))
            .ToListAsync(cancellationToken);

        var availableByItem = balances
            .GroupBy(x => x.ItemId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x =>
                    x.OnHandQuantity - x.ReservedQuantity));

        return items
            .Select(item =>
            {
                var available =
                    availableByItem.TryGetValue(
                        item.Id,
                        out var quantity)
                        ? quantity
                        : 0m;

                return new LowStockAlert
                {
                    ItemId = item.Id,
                    ItemCode = item.Code,
                    ItemName = item.Name,
                    UnitSymbol = item.Unit?.Symbol ?? "",
                    AvailableQuantity = available,
                    Threshold = item.LowStockThreshold
                };
            })
            .Where(x => x.AvailableQuantity <= x.Threshold)
            .OrderBy(x => x.AvailableQuantity)
            .ThenBy(x => x.ItemName)
            .ToList();
    }

    public async Task<bool> IsLowStockAsync(
        int itemId,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == itemId,
                cancellationToken);

        if (item == null || !item.EnableLowStockAlert)
            return false;

        var availableQuantity = await db.InventoryBalances
            .Where(x =>
                x.ItemId == itemId &&
                x.CustomerId == null)
            .SumAsync(
                x => x.OnHandQuantity - x.ReservedQuantity,
                cancellationToken);

        return availableQuantity <= item.LowStockThreshold;
    }

    public async Task<decimal> GetCompanyAvailableQuantityAsync(
        int itemId,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.InventoryBalances
            .Where(x =>
                x.ItemId == itemId &&
                x.CustomerId == null)
            .SumAsync(
                x => x.OnHandQuantity - x.ReservedQuantity,
                cancellationToken);
    }
}