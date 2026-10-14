using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using HamiChap_MES_ERP.Models.Sales.Enums;
using Microsoft.EntityFrameworkCore;
using HamiChap_MES_ERP.Models.Production.Quality;

namespace HamiChap_MES_ERP.Services;

public class ProductionOrderCompletionService
{
    private readonly ApplicationDbContext _db;

    public ProductionOrderCompletionService(
        ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task CompleteProductionOrderAsync(
        long productionOrderId)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var productionOrder =
                await _db.ProductionOrders
                    .Include(x => x.Lines)
                    .Include(x => x.SalesOrder)
                        .ThenInclude(x => x.Lines)
                    .FirstOrDefaultAsync(x =>
                        x.Id == productionOrderId);

            if (productionOrder == null)
                throw new InvalidOperationException(
                    "Production order not found.");

            if (productionOrder.Status ==
                ProductionOrderStatus.Completed)
            {
                return;
            }

            if (productionOrder.Status ==
                ProductionOrderStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    "Cancelled production orders cannot be completed.");
            }

            ValidateProductionQuantities(
                productionOrder);

            await ValidateQualityControlAsync(
                productionOrderId);

            productionOrder.Status =
                ProductionOrderStatus.Completed;

            productionOrder.ActualEndDate =
                DateTime.Now;

            await FulfillSalesOrderAsync(
                productionOrder);

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task ValidateQualityControlAsync(
    long productionOrderId)
    {
        var inspections =
            await _db.QualityInspections
                .Where(x =>
                    x.ProductionOrderId ==
                    productionOrderId)
                .ToListAsync();

        if (inspections.Count == 0)
        {
            throw new InvalidOperationException(
                "Production order has no quality inspection.");
        }

        var hasIncompleteInspection =
            inspections.Any(x =>
                x.Status ==
                    QualityInspectionStatus.Pending ||
                x.Status ==
                    QualityInspectionStatus.InProgress);

        if (hasIncompleteInspection)
        {
            throw new InvalidOperationException(
                "Production order has incomplete quality inspections.");
        }

        var hasFailedInspection =
            inspections.Any(x =>
                x.Status ==
                    QualityInspectionStatus.Failed);

        if (hasFailedInspection)
        {
            throw new InvalidOperationException(
                "Production order has failed quality inspection.");
        }
    }

    private static void ValidateProductionQuantities(
        ProductionOrder productionOrder)
    {
        if (productionOrder.Lines.Count == 0)
        {
            throw new InvalidOperationException(
                "Production order has no production lines.");
        }

        foreach (var line in productionOrder.Lines)
        {
            if (line.ProducedQuantity <
                line.PlannedQuantity)
            {
                throw new InvalidOperationException(
                    $"Production is incomplete for item {line.ItemId}. " +
                    $"Planned: {line.PlannedQuantity}, " +
                    $"Produced: {line.ProducedQuantity}.");
            }
        }
    }

    private async Task FulfillSalesOrderAsync(
        ProductionOrder productionOrder)
    {
        var salesOrder =
            productionOrder.SalesOrder;

        if (salesOrder == null)
        {
            throw new InvalidOperationException(
                "Production order is not linked to a sales order.");
        }

        foreach (var productionLine in productionOrder.Lines)
        {
            var salesLines =
                salesOrder.Lines
                    .Where(x =>
                        x.ItemId ==
                        productionLine.ItemId)
                    .ToList();

            var remainingQuantity =
                productionLine.ProducedQuantity;

            foreach (var salesLine in salesLines)
            {
                if (remainingQuantity <= 0)
                    break;

                var remainingSalesQuantity =
                    salesLine.Quantity -
                    salesLine.ProducedQuantity;

                if (remainingSalesQuantity <= 0)
                    continue;

                var quantityToFulfill =
                    Math.Min(
                        remainingQuantity,
                        remainingSalesQuantity);

                salesLine.ProducedQuantity +=
                    quantityToFulfill;

                remainingQuantity -=
                    quantityToFulfill;
            }
        }

        UpdateSalesOrderStatus(salesOrder);
    }

    private static void UpdateSalesOrderStatus(
    Models.Sales.SalesOrder salesOrder)
    {
        var allDelivered =
            salesOrder.Lines.Count > 0 &&
            salesOrder.Lines.All(x =>
                x.DeliveredQuantity >=
                x.Quantity);

        var anyDelivered =
            salesOrder.Lines.Any(x =>
                x.DeliveredQuantity > 0);

        if (allDelivered)
        {
            salesOrder.Status =
                SalesOrderStatus.Completed;
        }
        else if (anyDelivered)
        {
            salesOrder.Status =
                SalesOrderStatus.PartiallyDelivered;
        }
    }
}