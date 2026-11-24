using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using Microsoft.EntityFrameworkCore;


namespace HamiChap_MES_ERP.Services;


public class MrpService
{
    private readonly ApplicationDbContext _db;


    public MrpService(
        ApplicationDbContext db)
    {
        _db = db;
    }



    public async Task<MaterialRequirementPlan>
        CalculateAsync(
            long productionOrderId)
    {

        var order =
            await _db.ProductionOrders
            .Include(x => x.Lines)
            .ThenInclude(x => x.Item)
            .FirstAsync(x =>
                x.Id == productionOrderId);



        var plan =
            new MaterialRequirementPlan
            {
                ProductionOrderId =
                    productionOrderId
            };



        foreach (var product in order.Lines)
        {

            var bom =
                await _db.BillOfMaterials
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x =>
                    x.ItemId == product.ItemId &&
                    x.IsActive);



            if (bom == null)
                continue;



            foreach (var material in bom.Lines)
            {

                var required =
                    material.Quantity *
                    product.PlannedQuantity;



                var existing = plan.Lines.FirstOrDefault(x => x.ItemId == material.ItemId);
                if (existing != null)
                {
                    if (existing.UnitId != material.UnitId)
                        throw new InvalidOperationException($"Material {material.ItemId} has inconsistent BOM units.");
                    existing.RequiredQuantity += required;
                }
                else
                {
                    plan.Lines.Add(new MaterialRequirementPlanLine
                    {
                        ItemId = material.ItemId,
                        RequiredQuantity = required,
                        UnitId = material.UnitId
                    });
                }
            }
        }



        // Compare each material's combined demand with stock only once.
        foreach (var line in plan.Lines)
        {
            line.AvailableQuantity = await _db.InventoryBalances
                .Where(x => x.ItemId == line.ItemId && x.CustomerId == null)
                .SumAsync(x => x.OnHandQuantity - x.ReservedQuantity);
        }

        _db.MaterialRequirementPlans.Add(plan);


        await _db.SaveChangesAsync();


        return plan;
    }
}