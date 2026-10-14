using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class SalesDeliveryService
{
    private readonly ApplicationDbContext _db;
    private readonly InventoryPostingService _inventoryPosting;
    private readonly NumberSeriesService _numberSeries;

    public SalesDeliveryService(
        ApplicationDbContext db,
        InventoryPostingService inventoryPosting,
        NumberSeriesService numberSeries)
    {
        _db = db;
        _inventoryPosting = inventoryPosting;
        _numberSeries = numberSeries;
    }

    public async Task<SalesDelivery> CreateDeliveryAsync(
        long salesOrderId,
        int fromLocationId,
        int toLocationId,
        string userId,
        IEnumerable<SalesDeliveryLineRequest> lines)
    {
        var salesOrder =
            await _db.SalesOrders
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x =>
                    x.Id == salesOrderId);

        if (salesOrder == null)
            throw new InvalidOperationException(
                "Sales order not found.");

        if (salesOrder.Status ==
            SalesOrderStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancelled sales orders cannot be delivered.");
        }

        var requestedLines =
            lines.ToList();

        if (requestedLines.Count == 0)
            throw new InvalidOperationException(
                "At least one delivery line is required.");

        ValidateDeliveryLines(
            salesOrder,
            requestedLines);

        var delivery = new SalesDelivery
        {
            DeliveryNumber =
                await _numberSeries.GetNextNumberAsync(
                    NumberSeriesType.SalesDelivery),

            SalesOrderId =
                salesOrder.Id,

            CustomerId =
                salesOrder.CustomerId,

            DeliveryDate =
                DateTime.Now,

            FromLocationId =
                fromLocationId,

            ToLocationId =
                toLocationId,

            Status =
                SalesDeliveryStatus.Draft,

            UserId =
                userId
        };

        foreach (var request in requestedLines)
        {
            var salesLine =
                salesOrder.Lines.First(x =>
                    x.Id == request.SalesOrderLineId);

            delivery.Lines.Add(
                new SalesDeliveryLine
                {
                    SalesOrderLineId =
                        salesLine.Id,

                    ItemId =
                        salesLine.ItemId,

                    Quantity =
                        request.Quantity,

                    UnitId =
                        salesLine.UnitId,

                    Notes =
                        request.Notes ?? ""
                });
        }

        _db.SalesDeliveries.Add(delivery);

        await _db.SaveChangesAsync();

        return delivery;
    }

    public async Task PostDeliveryAsync(
        long deliveryId,
        string userId)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var delivery =
                await _db.SalesDeliveries
                    .Include(x => x.SalesOrder)
                        .ThenInclude(x => x.Lines)
                    .Include(x => x.Lines)
                    .FirstOrDefaultAsync(x =>
                        x.Id == deliveryId);

            if (delivery == null)
                throw new InvalidOperationException(
                    "Sales delivery not found.");

            if (delivery.Status ==
                SalesDeliveryStatus.Posted)
            {
                throw new InvalidOperationException(
                    "Sales delivery has already been posted.");
            }

            if (delivery.Status ==
                SalesDeliveryStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    "Cancelled delivery cannot be posted.");
            }

            ValidatePostedDeliveryLines(
                delivery.SalesOrder,
                delivery.Lines);

            foreach (var line in delivery.Lines)
            {
                var salesLine =
                    delivery.SalesOrder.Lines
                        .First(x =>
                            x.Id ==
                            line.SalesOrderLineId);

                salesLine.DeliveredQuantity +=
                    line.Quantity;

                var inventoryDocument =
                    new InventoryDocument
                    {
                        DocumentNumber =
                            await _numberSeries
                                .GetNextNumberAsync(
                                    NumberSeriesType.SalesDelivery),

                        DocumentType =
                            InventoryDocumentType.SalesDelivery,

                        DocumentDate =
                            delivery.DeliveryDate,

                        FromLocationId =
                            delivery.FromLocationId,

                        ToLocationId =
                            delivery.ToLocationId,

                        UserId =
                            userId,

                        Status =
                            InventoryDocumentStatus.Draft,

                        Notes =
                            $"Sales delivery {delivery.DeliveryNumber}"
                    };

                inventoryDocument.Lines.Add(
                    new InventoryDocumentLine
                    {
                        ItemId =
                            line.ItemId,

                        Quantity =
                            line.Quantity,

                        UnitId =
                            line.UnitId,

                        FromCustomerId =
                            null,

                        ToCustomerId =
                            delivery.CustomerId,

                        Notes =
                            line.Notes
                    });

                _db.InventoryDocuments.Add(
                    inventoryDocument);

                await _db.SaveChangesAsync();

                await _inventoryPosting.PostAsync(
                    inventoryDocument.Id,
                    userId,
                    useTransaction: false);

                line.InventoryDocumentId =
                    inventoryDocument.Id;
            }

            UpdateSalesOrderStatus(
                delivery.SalesOrder);

            delivery.Status =
                SalesDeliveryStatus.Posted;

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static void ValidateDeliveryLines(
    SalesOrder salesOrder,
    List<SalesDeliveryLineRequest> lines)
    {
        foreach (var request in lines)
        {
            if (request.Quantity <= 0)
            {
                throw new InvalidOperationException(
                    "Delivery quantity must be greater than zero.");
            }

            var salesLine =
                salesOrder.Lines
                    .FirstOrDefault(x =>
                        x.Id == request.SalesOrderLineId);

            if (salesLine == null)
            {
                throw new InvalidOperationException(
                    $"Sales order line {request.SalesOrderLineId} was not found.");
            }

            var remaining =
                salesLine.Quantity -
                salesLine.DeliveredQuantity;

            if (request.Quantity > remaining)
            {
                throw new InvalidOperationException(
                    $"Delivery quantity for item {salesLine.ItemId} " +
                    $"exceeds the remaining ordered quantity. " +
                    $"Remaining: {remaining}, " +
                    $"Requested: {request.Quantity}.");
            }

            var remainingProduced =
                salesLine.ProducedQuantity -
                salesLine.DeliveredQuantity;

            if (request.Quantity > remainingProduced)
            {
                throw new InvalidOperationException(
                    $"Delivery quantity for item {salesLine.ItemId} " +
                    $"exceeds the available produced quantity. " +
                    $"Produced remaining: {remainingProduced}, " +
                    $"Requested: {request.Quantity}.");
            }
        }
    }

    private static void ValidatePostedDeliveryLines(
    SalesOrder salesOrder,
    ICollection<SalesDeliveryLine> deliveryLines)
    {
        foreach (var deliveryLine in deliveryLines)
        {
            if (deliveryLine.Quantity <= 0)
            {
                throw new InvalidOperationException(
                    "Delivery quantity must be greater than zero.");
            }

            var salesLine =
                salesOrder.Lines
                    .FirstOrDefault(x =>
                        x.Id == deliveryLine.SalesOrderLineId);

            if (salesLine == null)
            {
                throw new InvalidOperationException(
                    $"Sales order line {deliveryLine.SalesOrderLineId} was not found.");
            }

            var remainingOrdered =
                salesLine.Quantity -
                salesLine.DeliveredQuantity;

            if (deliveryLine.Quantity > remainingOrdered)
            {
                throw new InvalidOperationException(
                    $"Delivery quantity for item {salesLine.ItemId} " +
                    $"exceeds the remaining ordered quantity. " +
                    $"Remaining: {remainingOrdered}, " +
                    $"Requested: {deliveryLine.Quantity}.");
            }

            var remainingProduced =
                salesLine.ProducedQuantity -
                salesLine.DeliveredQuantity;

            if (deliveryLine.Quantity > remainingProduced)
            {
                throw new InvalidOperationException(
                    $"Delivery quantity for item {salesLine.ItemId} " +
                    $"exceeds the available produced quantity. " +
                    $"Produced remaining: {remainingProduced}, " +
                    $"Requested: {deliveryLine.Quantity}.");
            }
        }
    }

    private static void UpdateSalesOrderStatus(
        SalesOrder salesOrder)
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

public class SalesDeliveryLineRequest
{
    public long SalesOrderLineId { get; set; }

    public decimal Quantity { get; set; }

    public string? Notes { get; set; }
}