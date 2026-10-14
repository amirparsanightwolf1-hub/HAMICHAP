using HamiChap_MES_ERP.Models.Customers;
using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

public class InventoryDocumentLine : BaseEntity
{
    public long Id { get; set; }

    public long InventoryDocumentId { get; set; }

    public InventoryDocument InventoryDocument { get; set; } = null!;


    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;


    // Customer ownership
    // null = company-owned
    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }


    public long? InventoryLotId { get; set; }

    public InventoryLot? InventoryLot { get; set; }


    public decimal Quantity { get; set; }


    public int UnitId { get; set; }

    public Unit Unit { get; set; } = null!;


    public string Notes { get; set; } = "";


    public string? LotNumber { get; set; }


    public DateTime? LotExpirationDate { get; set; }

    public int? FromCustomerId { get; set; }
    public Customer? FromCustomer { get; set; }

    public int? ToCustomerId { get; set; }
    public Customer? ToCustomer { get; set; }
}