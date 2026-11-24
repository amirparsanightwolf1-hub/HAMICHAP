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
    public string Zinc { get; set; } = "";
    // Named spot colors with user-chosen on-screen preview colors, stored in the job JSON.
    public List<RollPantoneColor> PantoneColors { get; set; } = new();
    public string PrintingNotes { get; set; } = "";
    public decimal? RollDiameter { get; set; }
    public string RollOrientation { get; set; } = "";
    public bool HandGluing { get; set; }
    public bool MachineGluing { get; set; }

    // Service choices are stored as structured values for future costing rules.
    public List<string> SelectedServices { get; set; } = new();
    public Dictionary<string, string> ToolingProviders { get; set; } = new();
}


/// <summary>
/// A Pantone reference entered by the printer, together with a separately selected
/// display swatch. The hex color is a screen preview, not an official color match.
/// </summary>
public sealed class RollPantoneColor
{
    public string Code { get; set; } = "";
    public string Hex { get; set; } = "#DB2635";

    public int? Cyan { get; set; }
    public int? Magenta { get; set; }
    public int? Yellow { get; set; }
    public int? Black { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string SafeHex => Cyan.HasValue && Magenta.HasValue && Yellow.HasValue && Black.HasValue
        ? PreviewHex(Cyan.Value, Magenta.Value, Yellow.Value, Black.Value)
        : IsValidHex(Hex) ? Hex : "#808080";

    public static string PreviewHex(int c, int m, int y, int k)
    {
        int Channel(int ink) => (int)Math.Round(255 * (1 - Math.Clamp(ink, 0, 100) / 100.0) * (1 - Math.Clamp(k, 0, 100) / 100.0));
        return $"#{Channel(c):X2}{Channel(m):X2}{Channel(y):X2}";
    }

    public void EnsureCmyk()
    {
        if (Cyan.HasValue && Magenta.HasValue && Yellow.HasValue && Black.HasValue) return;
        var hex = IsValidHex(Hex) ? Hex : "#808080";
        var r = Convert.ToInt32(hex.Substring(1, 2), 16) / 255.0;
        var g = Convert.ToInt32(hex.Substring(3, 2), 16) / 255.0;
        var b = Convert.ToInt32(hex.Substring(5, 2), 16) / 255.0;
        var k = 1 - Math.Max(r, Math.Max(g, b));
        int Ink(double channel) => k >= 1 ? 0 : (int)Math.Round(100 * (1 - channel - k) / (1 - k));
        Cyan = Ink(r); Magenta = Ink(g); Yellow = Ink(b); Black = (int)Math.Round(k * 100);
        Hex = SafeHex;
    }

    public static List<RollPantoneColor> FromLegacyReferences(string references) =>
        references.Split(new[] { ',', ';', '\n', '\r', '،' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(code => new RollPantoneColor { Code = code, Hex = "#808080" }).ToList();

    public static bool IsValidHex(string? value) =>
        value is { Length: 7 } && value[0] == '#' &&
        value.Skip(1).All(Uri.IsHexDigit);
}
