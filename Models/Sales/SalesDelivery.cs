using HamiChap_MES_ERP.Models.Customers;
using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public class SalesDelivery : BaseEntity
{
    public long Id { get; set; }

    public string DeliveryNumber { get; set; } = "";

    public long SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DateTime DeliveryDate { get; set; } = DateTime.Now;

    public int FromLocationId { get; set; }
    public InventoryLocation FromLocation { get; set; } = null!;

    public int ToLocationId { get; set; }
    public InventoryLocation ToLocation { get; set; } = null!;

    public SalesDeliveryStatus Status { get; set; }
        = SalesDeliveryStatus.Draft;

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public string Notes { get; set; } = "";

    public ICollection<SalesDeliveryLine> Lines { get; set; }
        = new List<SalesDeliveryLine>();
}