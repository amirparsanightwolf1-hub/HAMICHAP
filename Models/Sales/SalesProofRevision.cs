using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public enum ProofRevisionStatus { Sent = 1, Accepted = 2, ChangesRequested = 3, Superseded = 4 }

public sealed class SalesProofRevision : BaseEntity
{
    public long Id { get; set; }
    public long SalesRequestId { get; set; }
    public SalesRequest SalesRequest { get; set; } = null!;
    public int RevisionNumber { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public ProofRevisionStatus Status { get; set; }
    public DateTime SentAtUtc { get; set; }
    public string SentByUserId { get; set; } = "";
    public DateTime? DecisionAtUtc { get; set; }
    public string? DecisionByUserId { get; set; }
    public string DecisionNotes { get; set; } = "";
}
