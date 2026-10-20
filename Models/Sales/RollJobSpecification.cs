namespace HamiChap_MES_ERP.Models.Sales;

public sealed class RollJobSpecification
{
    public string JobTitle { get; set; } = "";

    // Reusable finished product in the inventory item master.
    public int? FinishedProductItemId { get; set; }
    // Relative URL of the artwork uploaded specifically for this job.
    public string ArtworkRelativePath { get; set; } = "";
    public string ArtworkOriginalFileName { get; set; } = "";
    // Multiple artwork files; singular properties above remain for backwards compatibility.
    public List<JobArtworkFile> ArtworkFiles { get; set; } = new();
    public decimal? LabelPull { get; set; }
    public decimal? LabelWidth { get; set; }
    public decimal? GapBetweenLabels { get; set; }
    public int? LabelQuantity { get; set; }
    public string PaperType { get; set; } = "";
    public int? ColorCount { get; set; }
    public string PrintingNotes { get; set; } = "";
    public decimal? RollDiameter { get; set; }
    public string RollOrientation { get; set; } = "";
    public bool HandGluing { get; set; }
    public bool MachineGluing { get; set; }

    // Service choices are stored as structured values for future costing rules.
    public List<string> SelectedServices { get; set; } = new();
}
