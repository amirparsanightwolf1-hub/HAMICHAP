using HamiChap_MES_ERP.Models.Inventory;

namespace HamiChap_MES_ERP.Services;

/// <summary>Only active storage locations and usable lots count as available stock.</summary>
public static class WarehouseStockPolicy
{
    public static IQueryable<InventoryBalance> Stored(this IQueryable<InventoryBalance> query) =>
        query.Where(x => x.InventoryLocation.IsActive &&
            (x.InventoryLocation.Type == InventoryLocationType.Warehouse ||
             x.InventoryLocation.Type == InventoryLocationType.FinishedGoods));

    public static IQueryable<InventoryBalance> UsableMaterials(this IQueryable<InventoryBalance> query, DateTime day) =>
        query.Where(x => x.InventoryLocation.IsActive &&
            x.InventoryLocation.Type == InventoryLocationType.Warehouse &&
            x.Item.IsActive &&
            (!x.Item.RequiresLotTracking ||
             (x.InventoryLot != null && !x.InventoryLot.IsClosed &&
              (!x.InventoryLot.ExpirationDate.HasValue || x.InventoryLot.ExpirationDate.Value >= day))));

    public static IQueryable<InventoryBalance> UsableStock(this IQueryable<InventoryBalance> query, DateTime day) =>
        query.Stored().Where(x => x.Item.IsActive &&
            (!x.Item.RequiresLotTracking ||
             (x.InventoryLot != null && !x.InventoryLot.IsClosed &&
              (!x.InventoryLot.ExpirationDate.HasValue || x.InventoryLot.ExpirationDate.Value >= day))));
}
