using HamiChap_MES_ERP.Models.Customers;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Inventory;

public class InventoryBalance : BaseEntity
{
    public long Id { get; set; }

    // Item
    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;


    // Customer ownership
    // null = company-owned inventory
    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }


    // Lot
    public long? InventoryLotId { get; set; }

    public InventoryLot? InventoryLot { get; set; }


    // Location
    public int InventoryLocationId { get; set; }

    public InventoryLocation InventoryLocation { get; set; } = null!;


    // Quantities
    public decimal OnHandQuantity { get; set; }

    public decimal ReservedQuantity { get; set; }

    public decimal AvailableQuantity =>
        OnHandQuantity - ReservedQuantity;


    public DateTime LastUpdated { get; set; }
        = DateTime.Now;
}