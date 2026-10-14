using HamiChap_MES_ERP.Models.Shared;
using HamiChap_MES_ERP.Models.Production;

namespace HamiChap_MES_ERP.Models.Production;

public class ProductionWorkCenter : BaseEntity
{
    public int Id { get; set; }


    public string Code { get; set; } = "";


    public string Name { get; set; } = "";


    public bool IsActive { get; set; } = true;



    public ICollection<ProductionOperation> Operations { get; set; }
        = new List<ProductionOperation>();
}