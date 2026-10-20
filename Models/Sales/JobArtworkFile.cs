namespace HamiChap_MES_ERP.Models.Sales;

/// <summary>One artwork file attached to a sales job.</summary>
public sealed class JobArtworkFile
{
    public string RelativePath { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
}
