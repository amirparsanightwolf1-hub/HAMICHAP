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


    /// <summary>
    /// Customer acceptance is the final sales step: accept the quotation and
    /// create its confirmed sales order atomically. Repeated calls reuse the
    /// order, including for quotations accepted before this workflow change.
    /// </summary>
    public async Task<SalesOrder> AcceptQuotationAndCreateOrderAsync(long quotationId)
    {
        // The serializable transaction protects the check/create sequence from
        // parallel acceptance requests and the number series already uses this
        // context's ambient EF transaction.
        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        try
        {
            var quotation = await _db.Quotations
                .FirstOrDefaultAsync(x => x.Id == quotationId)
                ?? throw new InvalidOperationException("Quotation not found.");

            var canAccept =
                quotation.Status == QuotationStatus.Approved ||
                quotation.Status == QuotationStatus.ReadyToSend ||
                quotation.Status == QuotationStatus.SentToCustomer ||
                quotation.Status == QuotationStatus.CustomerNegotiation ||
                quotation.Status == QuotationStatus.Accepted;

            if (!canAccept)
                throw new InvalidOperationException(
                    "Only an internally approved quotation may be accepted by the customer.");

            if (quotation.Status != QuotationStatus.Accepted)
            {
                quotation.Status = QuotationStatus.Accepted;
                await _db.SaveChangesAsync();
            }

            // This method checks for an existing SalesOrder by QuotationId.
            // If creation fails the acceptance status is rolled back as well.
            var order = await CreateFromQuotationAsync(quotationId);
            await transaction.CommitAsync();
            return order;
        }
        catch
        {
            await transaction.RollbackAsync();
            // A failed transaction must not leave Accepted/added order entities
            // tracked as if they had been committed.
            _db.ChangeTracker.Clear();
            throw;
        }
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
                    // Preserve the source request/proof through QuotationLine.
                    QuotationLineId = line.Id,
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