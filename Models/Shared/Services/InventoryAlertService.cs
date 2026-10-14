using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class InventoryAlertService
{
    private readonly ApplicationDbContext _db;

    public InventoryAlertService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<LowStockAlert>> GetLowStockAlertsAsync()
    {
        var items = await _db.Items
            .AsNoTracking()
            .Include(x => x.Unit)
            .Where(x => x.IsActive)
            .Where(x => x.EnableLowStockAlert)
            .ToListAsync();

        if (items.Count == 0)
            return new List<LowStockAlert>();

        var itemIds = items
            .Select(x => x.Id)
            .ToList();

        var balances = await _db.InventoryBalances
            .AsNoTracking()
            .Where(x =>
                x.CustomerId == null &&
                itemIds.Contains(x.ItemId))
            .ToListAsync();

        var availableByItem = balances
            .GroupBy(x => x.ItemId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x =>
                    x.OnHandQuantity -
                    x.ReservedQuantity));

        var alerts = items
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
            .Where(x =>
                x.AvailableQuantity <= x.Threshold)
            .OrderBy(x => x.AvailableQuantity)
            .ThenBy(x => x.ItemName)
            .ToList();

        return alerts;
    }

    public async Task<bool> IsLowStockAsync(int itemId)
    {
        var item = await _db.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == itemId);

        if (item == null ||
            !item.EnableLowStockAlert)
        {
            return false;
        }

        var availableQuantity =
            await _db.InventoryBalances
                .Where(x =>
                    x.ItemId == itemId &&
                    x.CustomerId == null)
                .SumAsync(x =>
                    x.OnHandQuantity -
                    x.ReservedQuantity);

        return availableQuantity <=
               item.LowStockThreshold;
    }

    public async Task<decimal>
        GetCompanyAvailableQuantityAsync(int itemId)
    {
        return await _db.InventoryBalances
            .Where(x =>
                x.ItemId == itemId &&
                x.CustomerId == null)
            .SumAsync(x =>
                x.OnHandQuantity -
                x.ReservedQuantity);
    }
}