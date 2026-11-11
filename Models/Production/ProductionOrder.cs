using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class ProductionOrder : BaseEntity
{
    public long Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public long SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;
    public ProductionOrderStatus Status { get; set; } = ProductionOrderStatus.Planned;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    // Planning preferences used by the Production Planning calendar.
    public int EstimatedDurationMinutes { get; set; } = 60;
    public bool AvoidOvertime { get; set; }
    public bool AvoidNightShift { get; set; }

    // Production Manager approval: applies to a fingerprint of the current ordered machine stages.
    public DateTime? OperationsSequenceConfirmedAt { get; set; }
    public string? OperationsSequenceConfirmedBy { get; set; }
    public string? OperationsSequenceSignature { get; set; }

    public string Notes { get; set; } = "";
    public ICollection<ProductionOrderLine> Lines { get; set; } = new List<ProductionOrderLine>();
}
