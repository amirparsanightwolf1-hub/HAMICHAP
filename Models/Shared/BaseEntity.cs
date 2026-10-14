using System.ComponentModel.DataAnnotations;

namespace HamiChap_MES_ERP.Models.Shared;

public abstract class BaseEntity
{
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}