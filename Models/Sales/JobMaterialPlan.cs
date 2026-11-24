namespace HamiChap_MES_ERP.Models.Sales;

public sealed class JobMaterialPlan
{
    public decimal WastePercent { get; set; } = 5;
    public string DimensionUnit { get; set; } = "";
    public int? Lanes { get; set; }
    public decimal? WebWidthMm { get; set; }
    public int? RollGrammage { get; set; }
    public int? LabelsPerFinishedRoll { get; set; }
    public int? FinishedQuantity { get; set; }
    public int? ProductsPerSheet { get; set; }
    public decimal? NetQuantity { get; set; }
    public decimal? GrossQuantity { get; set; }
    public decimal? AreaSquareMetres { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? FinishedRolls { get; set; }

    private void Clear() => NetQuantity = GrossQuantity = AreaSquareMetres = WeightKg = FinishedRolls = null;

    public void Calculate(RollJobSpecification job)
    {
        Clear();
        if (DimensionUnit is not ("mm" or "cm") || job.LabelPull is not > 0 ||
            job.LabelWidth is not > 0 || job.LabelQuantity is not > 0 ||
            job.GapBetweenLabels is not >= 0 || Lanes is not > 0 || WastePercent is < 0 or > 100)
            return;
        var scale = DimensionUnit == "cm" ? 10m : 1m;
        var width = job.LabelWidth.Value * scale;
        var pitch = (job.LabelPull.Value + job.GapBetweenLabels.Value) * scale;
        if (WebWidthMm is > 0 && WebWidthMm.Value < width * Lanes.Value) return;
        var repeats = Math.Ceiling((decimal)job.LabelQuantity.Value / Lanes.Value);
        var net = repeats * pitch / 1000m;
        NetQuantity = Math.Ceiling(net * 100m) / 100m;
        GrossQuantity = Math.Ceiling(net * (1 + WastePercent / 100m) * 100m) / 100m;
        if (WebWidthMm is > 0)
        {
            AreaSquareMetres = Math.Round(GrossQuantity.Value * WebWidthMm.Value / 1000m, 3);
            if (RollGrammage is > 0)
                WeightKg = Math.Round(AreaSquareMetres.Value * RollGrammage.Value / 1000m, 3);
        }
        if (LabelsPerFinishedRoll is > 0)
            FinishedRolls = Math.Ceiling((decimal)job.LabelQuantity.Value / LabelsPerFinishedRoll.Value);
    }

    public void Calculate(SheetJobSpecification job)
    {
        Clear();
        if (WastePercent is < 0 or > 100) return;
        if (FinishedQuantity.HasValue || ProductsPerSheet.HasValue)
        {
            if (FinishedQuantity is not > 0 || ProductsPerSheet is not > 0) return;
            NetQuantity = Math.Ceiling((decimal)FinishedQuantity.Value / ProductsPerSheet.Value);
        }
        else if (job.PrintRunSheets is > 0)
            NetQuantity = job.PrintRunSheets.Value;
        if (!NetQuantity.HasValue) return;
        GrossQuantity = Math.Ceiling(NetQuantity.Value * (1 + WastePercent / 100m));
        if (job.BeforeCutLengthCm is > 0 && job.BeforeCutWidthCm is > 0)
        {
            AreaSquareMetres = Math.Round(GrossQuantity.Value * job.BeforeCutLengthCm.Value * job.BeforeCutWidthCm.Value / 10000m, 3);
            if (job.Grammage is > 0)
                WeightKg = Math.Round(AreaSquareMetres.Value * job.Grammage.Value / 1000m, 3);
        }
    }
}
