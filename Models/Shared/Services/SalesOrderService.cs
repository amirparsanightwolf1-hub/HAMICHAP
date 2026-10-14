using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.EntityFrameworkCore;


namespace HamiChap_MES_ERP.Services;


public class SalesOrderService
{
    private readonly ApplicationDbContext _db;
    private readonly NumberSeriesService _numberSeries;


    public SalesOrderService(
        ApplicationDbContext db,
        NumberSeriesService numberSeries)
    {
        _db = db;
        _numberSeries = numberSeries;
    }


    public async Task<SalesOrder> CreateFromQuotationAsync(
        long quotationId)
    {
        var quotation = await _db.Quotations
            .Include(x => x.Lines)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x =>
                x.Id == quotationId);

        if (quotation == null)
            throw new InvalidOperationException(
                "Quotation not found.");

        if (quotation.Status != QuotationStatus.Accepted)
            throw new InvalidOperationException(
                "Quotation must be accepted by the customer.");

        var existingOrder = await _db.SalesOrders
            .FirstOrDefaultAsync(x =>
                x.QuotationId == quotation.Id);

        if (existingOrder != null)
            return existingOrder;

        if (!quotation.Lines.Any())
            throw new InvalidOperationException(
                "Cannot create a sales order without quotation lines.");

        var number =
            await _numberSeries.GetNextNumberAsync(
                NumberSeriesType.SalesOrder);

        var order = new SalesOrder
        {
            OrderNumber = number,
            QuotationId = quotation.Id,
            CustomerId = quotation.CustomerId,
            OrderDate = DateTime.Now,
            DeliveryDate =
        quotation.EstimatedDeliveryDays > 0
            ? DateTime.Now.Date.AddDays(
                quotation.EstimatedDeliveryDays)
            : null,
            TotalAmount = quotation.FinalAmount,
            Status = SalesOrderStatus.Confirmed,
            Notes = quotation.Notes
        };

        foreach (var line in quotation.Lines)
        {
            var item = await _db.Items
                .FirstOrDefaultAsync(x =>
                    x.Id == line.ItemId);

            if (item == null)
            {
                throw new InvalidOperationException(
                    $"Item {line.ItemId} was not found.");
            }

            order.Lines.Add(
                new SalesOrderLine
                {
                    ItemId = line.ItemId,
                    Quantity = line.Quantity,
                    UnitId = item.UnitId,
                    UnitPrice = line.UnitPrice,
                    ProducedQuantity = 0,
                    DeliveredQuantity = 0
                });
        }

        _db.SalesOrders.Add(order);

        await _db.SaveChangesAsync();

        return order;
    }
}