using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class SalesInvoiceService
{
    private readonly ApplicationDbContext _db;
    private readonly NumberSeriesService _numberSeries;

    public SalesInvoiceService(ApplicationDbContext db, NumberSeriesService numberSeries)
    {
        _db = db;
        _numberSeries = numberSeries;
    }

    // Must be called by SalesDeliveryService inside its existing posting transaction.
    public async Task<SalesInvoice> CreateForDeliveryAsync(long deliveryId, string userId)
    {
        if (_db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Invoice creation must run inside the delivery-posting transaction.");

        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("A valid user is required to create a sales invoice.");

        var existing = await _db.SalesInvoices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SalesDeliveryId == deliveryId);
        if (existing != null)
            throw new InvalidOperationException($"Delivery already has invoice {existing.InvoiceNumber}.");

        var delivery = await _db.SalesDeliveries
            .Include(x => x.SalesOrder).ThenInclude(x => x.Quotation).ThenInclude(x => x.Lines)
            .Include(x => x.SalesOrder).ThenInclude(x => x.Lines)
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == deliveryId);

        if (delivery == null)
            throw new InvalidOperationException("Sales delivery not found.");
        if (delivery.Status != SalesDeliveryStatus.Draft)
            throw new InvalidOperationException("Only a draft delivery can generate an invoice.");

        var order = delivery.SalesOrder;
        var quotation = order.Quotation;
        if (quotation.Status != QuotationStatus.Accepted)
            throw new InvalidOperationException("The linked quotation must be accepted before invoicing.");
        if (quotation.CustomerId != delivery.CustomerId || order.CustomerId != delivery.CustomerId)
            throw new InvalidOperationException("The delivery, order, and quotation customers do not match.");
        if (delivery.Lines.Count == 0)
            throw new InvalidOperationException("A delivery without lines cannot be invoiced.");

        var invoice = new SalesInvoice
        {
            InvoiceNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.SalesInvoice, useTransaction: false),
            QuotationId = quotation.Id,
            SalesOrderId = order.Id,
            SalesDeliveryId = delivery.Id,
            CustomerId = delivery.CustomerId,
            InvoiceDate = DateTime.Now,
            Status = SalesInvoiceStatus.Issued,
            CreatedByUserId = userId,
            Notes = $"Generated from delivery {delivery.DeliveryNumber}"
        };

        var quantityBeingInvoicedByQuotationLine = new Dictionary<long, decimal>();

        foreach (var deliveryLine in delivery.Lines)
        {
            if (deliveryLine.Quantity <= 0)
                throw new InvalidOperationException($"Delivery line for item {deliveryLine.ItemId} must have a positive quantity.");

            var orderLine = order.Lines.FirstOrDefault(x => x.Id == deliveryLine.SalesOrderLineId);
            if (orderLine == null || orderLine.ItemId != deliveryLine.ItemId)
                throw new InvalidOperationException($"Delivery line {deliveryLine.Id} does not match a valid sales order line.");

            var quoteLine = ResolveQuotationLine(quotation, orderLine);
            var alreadyInvoiced = await _db.SalesInvoiceLines
                .Where(x => x.QuotationLineId == quoteLine.Id && x.SalesInvoice.Status != SalesInvoiceStatus.Cancelled)
                .SumAsync(x => (decimal?)x.Quantity) ?? 0m;

            quantityBeingInvoicedByQuotationLine.TryGetValue(
                quoteLine.Id,
                out var alreadyAddedForThisDelivery);

            if (alreadyInvoiced + alreadyAddedForThisDelivery + deliveryLine.Quantity > quoteLine.Quantity)
                throw new InvalidOperationException(
                    $"Invoice quantity for quotation item {quoteLine.ItemId} exceeds accepted quantity. " +
                    $"Accepted: {quoteLine.Quantity}, already invoiced: {alreadyInvoiced + alreadyAddedForThisDelivery}, " +
                    $"requested: {deliveryLine.Quantity}.");

            quantityBeingInvoicedByQuotationLine[quoteLine.Id] =
                alreadyAddedForThisDelivery + deliveryLine.Quantity;

            var total = decimal.Round(deliveryLine.Quantity * quoteLine.UnitPrice, 2, MidpointRounding.AwayFromZero);
            invoice.Lines.Add(new SalesInvoiceLine
            {
                SalesDeliveryLineId = deliveryLine.Id,
                QuotationLineId = quoteLine.Id,
                ItemId = quoteLine.ItemId,
                UnitId = deliveryLine.UnitId,
                Quantity = deliveryLine.Quantity,
                UnitPrice = quoteLine.UnitPrice,
                LineTotal = total,
                Notes = string.IsNullOrWhiteSpace(deliveryLine.Notes) ? quoteLine.Notes : deliveryLine.Notes
            });
        }

        invoice.Subtotal = invoice.Lines.Sum(x => x.LineTotal);
        invoice.TotalAmount = invoice.Subtotal;
        _db.SalesInvoices.Add(invoice);
        await _db.SaveChangesAsync();
        return invoice;
    }

    private static QuotationLine ResolveQuotationLine(Quotation quotation, SalesOrderLine orderLine)
    {
        if (orderLine.QuotationLineId.HasValue)
        {
            var mapped = quotation.Lines.FirstOrDefault(x => x.Id == orderLine.QuotationLineId.Value);
            if (mapped == null || mapped.ItemId != orderLine.ItemId)
                throw new InvalidOperationException($"Sales order line {orderLine.Id} does not map to a valid quotation line.");
            return mapped;
        }

        var candidates = quotation.Lines.Where(x => x.ItemId == orderLine.ItemId).ToList();
        if (candidates.Count == 1)
            return candidates[0];
        if (candidates.Count == 0)
            throw new InvalidOperationException($"No quotation line was found for item {orderLine.ItemId}.");

        throw new InvalidOperationException(
            $"The accepted quotation contains multiple lines for item {orderLine.ItemId}, but this older sales order has no quotation-line mapping. Resolve the mapping before invoicing.");
    }
}
