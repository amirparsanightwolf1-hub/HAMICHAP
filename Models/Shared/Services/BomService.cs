using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore;
using HamiChap_MES_ERP.Models.Production;


namespace HamiChap_MES_ERP.Services;


public class BomService
{
    private readonly ApplicationDbContext _db;


    public BomService(
        ApplicationDbContext db)
    {
        _db = db;
    }



    public async Task<List<MaterialRequirement>>
        CalculateRequirementsAsync(
            int productId,
            decimal quantity)
    {

        var bom =
            await _db.BillOfMaterials
            .Include(x => x.Lines)
            .ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x =>
                x.ItemId == productId &&
                x.IsActive);



        if (bom == null)
            throw new Exception(
                "BOM not found");



        return bom.Lines
            .Select(x => new MaterialRequirement
            {
                ItemId = x.ItemId,

                ItemName = x.Item.Name,

                RequiredQuantity =
                    x.Quantity * quantity
            })
            .ToList();
    }
}