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


    public async Task RecordOutputAsync(
        long productionOrderId,
        long productionOrderLineId,
        decimal quantity,
        decimal wasteQuantity,
        string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException(
                "A valid user is required.");

        if (quantity <= 0)
            throw new InvalidOperationException(
                "Produced quantity must be greater than zero.");

        if (wasteQuantity < 0)
            throw new InvalidOperationException(
                "Waste quantity cannot be negative.");

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var order = await _db.ProductionOrders
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x =>
                    x.Id == productionOrderId);

            if (order == null)
                throw new InvalidOperationException(
                    "Production order not found.");

            if (order.Status != ProductionOrderStatus.InProduction)
                throw new InvalidOperationException(
                    "Output can only be recorded while the order is In Production.");

            var line = order.Lines.FirstOrDefault(x =>
                x.Id == productionOrderLineId);

            if (line == null)
                throw new InvalidOperationException(
                    "The production order line was not found.");

            if (line.ProducedQuantity + quantity >
                line.PlannedQuantity)
            {
                throw new InvalidOperationException(
                    $"Output exceeds the planned quantity. " +
                    $"Remaining: {line.PlannedQuantity - line.ProducedQuantity}.");
            }

            var output = new ProductionOutput
            {
                ProductionOrderId = order.Id,
                ItemId = line.ItemId,
                Quantity = quantity,
                WasteQuantity = wasteQuantity,
                ProductionDate = DateTime.Now,
                UserId = userId
            };

            _db.Set<ProductionOutput>().Add(output);

            line.ProducedQuantity += quantity;

            if (order.Lines.All(x =>
                x.ProducedQuantity >= x.PlannedQuantity))
            {
                order.Status = ProductionOrderStatus.QualityControl;
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
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