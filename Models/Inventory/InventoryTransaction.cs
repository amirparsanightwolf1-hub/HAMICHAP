using HamiChap_MES_ERP.Models.Administration;
using HamiChap_MES_ERP.Models.Customers;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Inventory;

public class InventoryTransaction : BaseEntity
{
    public long Id { get; set; }

    // Item being moved
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public long? InventoryLotId { get; set; }

    public InventoryLot? InventoryLot { get; set; }

    // Where it came from
    public int? FromLocationId { get; set; }
    public InventoryLocation? FromLocation { get; set; }

    // Where it went
    public int? ToLocationId { get; set; }
    public InventoryLocation? ToLocation { get; set; }

    // Customer that owns this stock.
    // null = company-owned inventory.
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    // Always positive
    public decimal Quantity { get; set; }

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public InventoryTransactionType TransactionType { get; set; }

    public DateTime TransactionDate { get; set; }
        = DateTime.Now;

    // Which user performed it
    public string UserId { get; set; } = "";

    public ApplicationUser User { get; set; } = null!;

    public long? ReferenceId { get; set; }

    public string Notes { get; set; } = "";

    public long InventoryDocumentLineId { get; set; }

    public InventoryDocumentLine InventoryDocumentLine { get; set; } = null!;

    public DateTime CreatedOn { get; set; } = DateTime.Now;

    public DateTime? ModifiedOn { get; set; }
}