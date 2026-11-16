using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

/// <summary>Append-only operator audit trail. All timestamps are UTC.</summary>
public class ProductionOperationEvent : BaseEntity
{
    public long Id { get; set; }
    public long ProductionOperationId { get; set; }
    public ProductionOperation ProductionOperation { get; set; } = null!;
    public string EventType { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string OperatorUserId { get; set; } = "";
    public string Reason { get; set; } = "";
}
