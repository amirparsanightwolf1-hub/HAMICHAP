using HamiChap_MES_ERP.Models.Production;
using HamiChap_MES_ERP.Models.Production.Quality;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Inventory;

public class InventoryDocument : BaseEntity
{
    public long Id { get; set; }

    public string DocumentNumber { get; set; } = "";

    public InventoryDocumentType DocumentType { get; set; }

    public DateTime DocumentDate { get; set; } = DateTime.Now;

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public string? Notes { get; set; }

    public int? FromLocationId { get; set; }

    public InventoryLocation? FromLocation { get; set; }


    public int? ToLocationId { get; set; }

    public InventoryLocation? ToLocation { get; set; }

    public ICollection<InventoryDocumentLine> Lines { get; set; }
        = new List<InventoryDocumentLine>();

    public InventoryDocumentStatus Status { get; set; }
    = InventoryDocumentStatus.Draft;
    public long? ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    public long? QualityInspectionId { get; set; }
    public QualityInspection? QualityInspection { get; set; }
}