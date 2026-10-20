namespace HamiChap_MES_ERP.Models.Sales;

public sealed class SheetJobSpecification
{
    public string JobTitle { get; set; } = "";

    // Reusable finished product in the inventory item master.
    public int? FinishedProductItemId { get; set; }
    // Relative URL of the artwork uploaded specifically for this job.
    public string ArtworkRelativePath { get; set; } = "";
    public string ArtworkOriginalFileName { get; set; } = "";
    // Multiple artwork files; singular properties above remain for backwards compatibility.\n
    public List<JobArtworkFile> ArtworkFiles { get; set; } = new();
    public string Referrer { get; set; } = "";
    public string RequiredItems { get; set; } = "";
    public string PaperType { get; set; } = "";
    public string Zinc { get; set; } = "";
    public string Die { get; set; } = "";
    public string Cliche { get; set; } = "";
    public string ProductionLocation { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public string PrintingNotes { get; set; } = "";
    public string OrderNumber { get; set; } = "";

    public int? PrintRunSheets { get; set; }
    public decimal? CutLengthCm { get; set; }
    public decimal? CutWidthCm { get; set; }
    public string SheetOrientation { get; set; } = "";
    public int? Grammage { get; set; }
    public int? SheetCount { get; set; }

    // Services, finishings, and print-color choices; pricing workflow remains shared.
    public List<string> SelectedServices { get; set; } = new();
}
