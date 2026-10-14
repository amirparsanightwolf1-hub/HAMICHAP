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



                var available =
                    await _db.InventoryBalances
                    .Where(x =>
                        x.ItemId == material.ItemId &&
                        x.CustomerId == null)
                    .SumAsync(x =>
                        x.OnHandQuantity -
                        x.ReservedQuantity);



                plan.Lines.Add(
                    new MaterialRequirementPlanLine
                    {
                        ItemId =
                            material.ItemId,

                        RequiredQuantity =
                            required,

                        AvailableQuantity =
                            available,

                        UnitId =
                            material.UnitId
                    });
            }
        }



        _db.MaterialRequirementPlans.Add(plan);


        await _db.SaveChangesAsync();


        return plan;
    }
}