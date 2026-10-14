using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production.Quality;

public class QualityInspection : BaseEntity
{
    public long Id { get; set; }

    public long ProductionOrderId { get; set; }

    public ProductionOrder ProductionOrder { get; set; } = null!;

    public int ItemId { get; set; }

    public decimal InspectedQuantity { get; set; }

    public decimal AcceptedQuantity { get; set; }

    public decimal RejectedQuantity { get; set; }

    public decimal ReworkQuantity { get; set; }

    public QualityInspectionStatus Status { get; set; }
        = QualityInspectionStatus.Pending;

    public string? InspectorUserId { get; set; }

    public DateTime InspectionDate { get; set; }
        = DateTime.Now;

    public string Notes { get; set; } = "";

    public ICollection<QualityInspectionResult> Results { get; set; }
        = new List<QualityInspectionResult>();
}