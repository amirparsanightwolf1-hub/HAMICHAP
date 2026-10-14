using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class ProductionOrderService
{
    private readonly ApplicationDbContext _db;
    private readonly NumberSeriesService _numberSeries;

    public ProductionOrderService(
        ApplicationDbContext db,
        NumberSeriesService numberSeries)
    {
        _db = db;
        _numberSeries = numberSeries;
    }

    public async Task<ProductionOrder> CreateFromSalesOrderAsync(
        long salesOrderId)
    {
        var salesOrder = await _db.SalesOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x =>
                x.Id == salesOrderId);

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
            await _db.ProductionOrders
                .FirstOrDefaultAsync(x =>
                    x.SalesOrderId == salesOrder.Id &&
                    x.Status != ProductionOrderStatus.Cancelled);

        if (existingProductionOrder != null)
        {
            return existingProductionOrder;
        }

        var productionNumber =
            await _numberSeries.GetNextNumberAsync(
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

            var item = await _db.Items
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

        _db.ProductionOrders.Add(productionOrder);

        await _db.SaveChangesAsync();

        return productionOrder;
    }
}