using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public sealed class ProductionOrderService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<ProductionOrder> CreateFromSalesOrderAsync(
        long salesOrderId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Lock the parent so concurrent requests check and create the order in sequence.
        var salesOrder = await db.SalesOrders
            .FromSqlInterpolated($"""
                SELECT * FROM [SalesOrders] WITH (UPDLOCK, HOLDLOCK)
                WHERE [Id] = {salesOrderId}
                """)
            .Include(x => x.Lines)
            .SingleOrDefaultAsync();

        if (salesOrder == null)
        {
            throw new InvalidOperationException(
                "Sales order not found.");
        }

        if (salesOrder.Status == SalesOrderStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancelled sales orders cannot create production orders.");
        }

        if (!salesOrder.Lines.Any())
        {
            throw new InvalidOperationException(
                "Sales order has no lines.");
        }

        var existingProductionOrder =
            await db.ProductionOrders
                .FirstOrDefaultAsync(x =>
                    x.SalesOrderId == salesOrder.Id &&
                    x.Status != ProductionOrderStatus.Cancelled);

        if (existingProductionOrder != null)
        {
            return existingProductionOrder;
        }

        var productionNumber =
            await new NumberSeriesService(db).GetNextNumberAsync(
                NumberSeriesType.ProductionOrder);

        var productionOrder = new ProductionOrder
        {
            OrderNumber = productionNumber,

            SalesOrderId = salesOrder.Id,

            Status = ProductionOrderStatus.Planned,

            CreatedDate = DateTime.Now,

            PlannedStartDate = null,

            PlannedEndDate = salesOrder.DeliveryDate,

            Notes =
                $"Created from Sales Order {salesOrder.OrderNumber}"
        };

        foreach (var salesLine in salesOrder.Lines)
        {
            var remainingQuantity =
                salesLine.Quantity -
                salesLine.ProducedQuantity;

            if (remainingQuantity <= 0)
                continue;

            var item = await db.Items
                .FirstOrDefaultAsync(x =>
                    x.Id == salesLine.ItemId);

            if (item == null)
            {
                throw new InvalidOperationException(
                    $"Item {salesLine.ItemId} was not found.");
            }

            productionOrder.Lines.Add(
                new ProductionOrderLine
                {
                    ItemId = salesLine.ItemId,

                    PlannedQuantity =
                        remainingQuantity,

                    ProducedQuantity = 0,

                    UnitId = item.UnitId
                });
        }

        if (!productionOrder.Lines.Any())
        {
            throw new InvalidOperationException(
                "No remaining quantity requires production.");
        }

        db.ProductionOrders.Add(productionOrder);

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return productionOrder;
    }
}