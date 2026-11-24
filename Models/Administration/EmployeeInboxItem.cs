using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Administration;

public sealed class EmployeeInboxItem : BaseEntity
{
    public long Id { get; set; }
    public string RecipientUserId { get; set; } = "";
    public string? SenderUserId { get; set; }
    public string EventKey { get; set; } = "";
    public string Kind { get; set; } = "message";
    public long? SourceId { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string? SubjectFa { get; set; }
    public string? BodyFa { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
