using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

/// <summary>Operator-submitted request for materials. This is not an inventory reservation.</summary>
public class ProductionMaterialRequest : BaseEntity
{
    public long Id { get; set; }
    public long ProductionOrderId { get; set; }
    public ProductionOrder ProductionOrder { get; set; } = null!;
    public DateTime RequestedDate { get; set; } = DateTime.Now;
    public string Status { get; set; } = "Requested";
    public ICollection<ProductionMaterialRequestLine> Lines { get; set; } = new List<ProductionMaterialRequestLine>();
}
