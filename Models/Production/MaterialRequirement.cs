namespace HamiChap_MES_ERP.Models.Production;

public class MaterialRequirement
{
    public int ItemId { get; set; }

    public string ItemName { get; set; } = "";

    public decimal RequiredQuantity { get; set; }
}