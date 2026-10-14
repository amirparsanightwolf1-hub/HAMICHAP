namespace HamiChap_MES_ERP.Models.Shared;

public class NumberSeries 
{
    public int Id { get; set; }

    // GR, CR, MT...
    public string Prefix { get; set; } = "";

    // Inventory, Production...
    public NumberSeriesType Type { get; set; }

    // Current year
    public int Year { get; set; }

    // Last used number
    public int LastNumber { get; set; }

    // Number of digits
    public int Padding { get; set; } = 6;

    public bool IsActive { get; set; } = true;
}