using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class StockBalanceService
{
    private readonly ApplicationDbContext _db;

    public StockBalanceService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<StockBalance?> GetItemBalanceAsync(int itemId)
    {
        var item = await _db.Items
            .FirstOrDefaultAsync(x => x.Id == itemId);

        if (item == null)
            return null;

        decimal onHand = await CalculateOnHand(itemId);

        return new StockBalance
        {
            ItemId = item.Id,
            ItemCode = item.Code,
            ItemName = item.Name,
            OnHand = onHand,
            Reserved = 0
        };
    }

    public async Task<List<StockBalance>> GetAllBalancesAsync()
    {
        var items = await _db.Items
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        var result = new List<StockBalance>();

        foreach (var item in items)
        {
            var balance = await GetItemBalanceAsync(item.Id);

            if (balance != null)
                result.Add(balance);
        }

        return result;
    }

    private async Task<decimal> CalculateOnHand(int itemId)
    {
        var transactions = await _db.InventoryTransactions
            .Where(x => x.ItemId == itemId)
            .ToListAsync();

        decimal qty = 0;

        foreach (var t in transactions)
        {
            switch (t.TransactionType)
            {
                case InventoryTransactionType.Receive:
                    qty += t.Quantity;
                    break;

                case InventoryTransactionType.Produce:
                    qty += t.Quantity;
                    break;

                case InventoryTransactionType.Return:
                    qty += t.Quantity;
                    break;

                case InventoryTransactionType.Consume:
                    qty -= t.Quantity;
                    break;

                case InventoryTransactionType.Scrap:
                    qty -= t.Quantity;
                    break;

                case InventoryTransactionType.Transfer:
                    break;

                case InventoryTransactionType.Adjustment:
                    qty += t.Quantity;
                    break;
            }
        }

        return qty;
    }

    public async Task<List<WarehouseStockBalance>> GetWarehouseBalancesAsync()
    {
        var transactions = await _db.InventoryTransactions
            .Include(x => x.Item)
            .Include(x => x.ToLocation)
            .Include(x => x.FromLocation)
            .ToListAsync();

        var balances = new Dictionary<(int ItemId, int? LocationId), WarehouseStockBalance>();

        foreach (var t in transactions)
        {
            //--------------------------------------------------
            // Incoming Stock
            //--------------------------------------------------

            if (t.ToLocationId != null)
            {
                var key = (t.ItemId, t.ToLocationId);

                if (!balances.ContainsKey(key))
                {
                    balances[key] = new WarehouseStockBalance
                    {
                        ItemId = t.ItemId,
                        ItemCode = t.Item.Code,
                        ItemName = t.Item.Name,
                        LocationId = t.ToLocationId,
                        LocationName = t.ToLocation?.Name ?? "",
                        OnHand = 0
                    };
                }

                balances[key].OnHand += t.Quantity;
            }

            //--------------------------------------------------
            // Outgoing Stock
            //--------------------------------------------------

            if (t.FromLocationId != null)
            {
                var key = (t.ItemId, t.FromLocationId);

                if (!balances.ContainsKey(key))
                {
                    balances[key] = new WarehouseStockBalance
                    {
                        ItemId = t.ItemId,
                        ItemCode = t.Item.Code,
                        ItemName = t.Item.Name,
                        LocationId = t.FromLocationId,
                        LocationName = t.FromLocation?.Name ?? "",
                        OnHand = 0
                    };
                }

                balances[key].OnHand -= t.Quantity;
            }
        }

        return balances.Values
            .OrderBy(x => x.ItemCode)
            .ThenBy(x => x.LocationName)
            .ToList();
    }

    public async Task<List<LotStockBalance>> GetLotBalancesAsync()
    {
        var transactions = await _db.InventoryTransactions
            .Include(x => x.Item)
            .Include(x => x.InventoryLot)
            .Include(x => x.ToLocation)
            .Include(x => x.FromLocation)
            .ToListAsync();

        var balances = new Dictionary<(int ItemId, long LotId, int? LocationId), LotStockBalance>();

        foreach (var t in transactions)
        {
            if (t.InventoryLotId == null)
                continue;

            //------------------------------------------
            // Incoming
            //------------------------------------------

            if (t.ToLocationId != null)
            {
                var key = (t.ItemId,
                           t.InventoryLotId.Value,
                           t.ToLocationId);

                if (!balances.ContainsKey(key))
                {
                    balances[key] = new LotStockBalance
                    {
                        ItemId = t.ItemId,
                        ItemCode = t.Item.Code,
                        ItemName = t.Item.Name,

                        InventoryLotId = t.InventoryLotId.Value,

                        LotNumber = t.InventoryLot?.LotNumber ?? "",

                        ExpirationDate = t.InventoryLot?.ExpirationDate,

                        LocationId = t.ToLocationId,

                        LocationName = t.ToLocation?.Name ?? "",

                        Quantity = 0
                    };
                }

                balances[key].Quantity += t.Quantity;
            }

            //------------------------------------------
            // Outgoing
            //------------------------------------------

            if (t.FromLocationId != null)
            {
                var key = (t.ItemId,
                           t.InventoryLotId.Value,
                           t.FromLocationId);

                if (!balances.ContainsKey(key))
                {
                    balances[key] = new LotStockBalance
                    {
                        ItemId = t.ItemId,
                        ItemCode = t.Item.Code,
                        ItemName = t.Item.Name,

                        InventoryLotId = t.InventoryLotId.Value,

                        LotNumber = t.InventoryLot?.LotNumber ?? "",

                        ExpirationDate = t.InventoryLot?.ExpirationDate,

                        LocationId = t.FromLocationId,

                        LocationName = t.FromLocation?.Name ?? "",

                        Quantity = 0
                    };
                }

                balances[key].Quantity -= t.Quantity;
            }
        }

        return balances.Values
            .Where(x => x.Quantity != 0)
            .OrderBy(x => x.ItemCode)
            .ThenBy(x => x.LocationName)
            .ThenBy(x => x.ExpirationDate)
            .ThenBy(x => x.LotNumber)
            .ToList();
    }

    public async Task<List<WarehouseStockBalance>> GetWarehouseBalancesFromSnapshotAsync()
    {
        return await _db.InventoryBalances

            .Include(x => x.Item)

            .Include(x => x.InventoryLocation)

            .Where(x => x.OnHandQuantity != 0)

            .Select(x => new WarehouseStockBalance
            {
                ItemId = x.ItemId,

                ItemCode = x.Item.Code,

                ItemName = x.Item.Name,

                LocationId = x.InventoryLocationId,

                LocationName = x.InventoryLocation != null
                    ? x.InventoryLocation.Name
                    : "",

                OnHand = x.OnHandQuantity,

                Reserved = x.ReservedQuantity
            })

            .OrderBy(x => x.ItemCode)

            .ThenBy(x => x.LocationName)

            .ToListAsync();
    }

    public async Task<List<LotStockBalance>> GetLotBalancesFromSnapshotAsync()
    {
        return await _db.InventoryBalances

            .Include(x => x.Item)

            .Include(x => x.InventoryLot)

            .Include(x => x.InventoryLocation)

            .Where(x => x.OnHandQuantity != 0)

            .Select(x => new LotStockBalance
            {
                ItemId = x.ItemId,

                ItemCode = x.Item.Code,

                ItemName = x.Item.Name,

                InventoryLotId = x.InventoryLotId ?? 0,

                LotNumber = x.InventoryLot != null
                    ? x.InventoryLot.LotNumber
                    : "",

                ExpirationDate = x.InventoryLot != null
                    ? x.InventoryLot.ExpirationDate
                    : null,

                LocationId = x.InventoryLocationId,

                LocationName = x.InventoryLocation != null
                    ? x.InventoryLocation.Name
                    : "",

                Quantity = x.OnHandQuantity
            })

            .OrderBy(x => x.ItemCode)

            .ThenBy(x => x.LocationName)

            .ThenBy(x => x.LotNumber)

            .ToListAsync();
    }

}