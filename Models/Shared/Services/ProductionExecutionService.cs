using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using Microsoft.EntityFrameworkCore;


namespace HamiChap_MES_ERP.Services;


public class ProductionExecutionService
{
    private readonly ApplicationDbContext _db;


    public ProductionExecutionService(
        ApplicationDbContext db)
    {
        _db = db;
    }



    public async Task StartOperationAsync(
        long operationId)
    {
        var operation =
            await _db.ProductionOperations
            .FirstAsync(x =>
                x.Id == operationId);


        operation.Status =
            ProductionOperationStatus.Running;


        operation.ProductionOrder.Status =
            ProductionOrderStatus.InProduction;


        await _db.SaveChangesAsync();
    }



    public async Task CompleteOperationAsync(
        long operationId)
    {
        var operation =
            await _db.ProductionOperations
            .FirstAsync(x =>
                x.Id == operationId);


        operation.Status =
            ProductionOperationStatus.Completed;


        await _db.SaveChangesAsync();
    }
}