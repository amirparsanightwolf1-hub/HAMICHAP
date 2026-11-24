using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using Microsoft.EntityFrameworkCore;
namespace HamiChap_MES_ERP.Services;

public static class ShipmentReadiness
{
    // Validate quantities and physical stock before invoicing and again at shipment.
    // QC receipts are the evidence of accepted output; do not invoice projected output.
    public static async Task ValidateAsync(ApplicationDbContext db, SalesDelivery delivery, SalesInvoice? invoice)
    {
        if (delivery.Status != SalesDeliveryStatus.Draft || delivery.SalesOrder.Status == SalesOrderStatus.Cancelled)
            throw new InvalidOperationException("Shipment or sales order is closed.");
        if (!await db.InventoryLocations.AnyAsync(x => x.Id == delivery.FromLocationId && x.IsActive && x.Type == InventoryLocationType.FinishedGoods) ||
            !await db.InventoryLocations.AnyAsync(x => x.Id == delivery.ToLocationId && x.IsActive && x.Type == InventoryLocationType.Customer &&
                (x.CustomerId == null || x.CustomerId == delivery.CustomerId)))
            throw new InvalidOperationException("Select an active finished goods warehouse and the correct customer destination.");
        if (delivery.Lines.Count == 0 || delivery.Lines.Any(x => x.Quantity <= 0 || decimal.Round(x.Quantity,3) != x.Quantity))
            throw new InvalidOperationException("Shipment quantities must be positive, with up to three decimal places.");
        if (invoice != null && (invoice.CustomerId != delivery.CustomerId || invoice.SalesOrderId != delivery.SalesOrderId ||
            invoice.Lines.Count != delivery.Lines.Count || delivery.Lines.Any(x => !invoice.Lines.Any(i =>
                i.SalesDeliveryLineId == x.Id && i.ItemId == x.ItemId && i.UnitId == x.UnitId && i.Quantity == x.Quantity))))
            throw new InvalidOperationException("Shipment does not match the invoiced items and quantities.");
        foreach (var group in delivery.Lines.GroupBy(x => x.SalesOrderLineId))
        {
            var line = delivery.SalesOrder.Lines.FirstOrDefault(x => x.Id == group.Key);
            if (line == null || group.Any(x => x.ItemId != line.ItemId || x.UnitId != line.UnitId) ||
                group.Sum(x => x.Quantity) > line.Quantity - line.DeliveredQuantity)
                throw new InvalidOperationException("Shipment exceeds the remaining order quantity.");
        }
        foreach (var group in delivery.Lines.GroupBy(x => x.ItemId))
        {
            var quantity = group.Sum(x => x.Quantity);
            var itemId = group.Key;
            var item = await db.Items.AsNoTracking().FirstAsync(x => x.Id == itemId);
            if (!item.IsActive || group.Any(x => x.UnitId != item.UnitId))
                throw new InvalidOperationException("Invalid finished goods item or unit.");
            // Current shipment posting has no lot allocator; refuse rather than substitute a lot.
            if (item.RequiresLotTracking)
                throw new InvalidOperationException($"Lot-tracked finished item {item.Name} requires a lot allocation before shipment.");
            var accepted = await db.InventoryDocumentLines.AsNoTracking().Where(x =>
                x.ItemId == itemId && x.CustomerId == null && x.InventoryDocument.Status == InventoryDocumentStatus.Posted &&
                x.InventoryDocument.DocumentType == InventoryDocumentType.ProductionReceipt && x.InventoryDocument.QualityInspectionId.HasValue &&
                x.InventoryDocument.ProductionOrder != null && x.InventoryDocument.ProductionOrder.SalesOrderId == delivery.SalesOrderId)
                .SumAsync(x => (decimal?)x.Quantity) ?? 0m;
            var allocated = await db.SalesInvoiceLines.AsNoTracking().Where(x => x.ItemId == itemId &&
                x.SalesInvoice.SalesOrderId == delivery.SalesOrderId && x.SalesInvoice.Status != SalesInvoiceStatus.Cancelled &&
                x.SalesInvoice.SalesDeliveryId != delivery.Id).SumAsync(x => (decimal?)x.Quantity) ?? 0m;
            if (accepted - allocated < quantity)
                throw new InvalidOperationException($"Not enough QC-accepted output for {item.Name}. Available to invoice: {Math.Max(0, accepted - allocated):N3}.");
            var physical = await db.InventoryBalances.UsableStock(DateTime.Today).AsNoTracking().Where(x =>
                x.ItemId == itemId && x.CustomerId == null && x.InventoryLocationId == delivery.FromLocationId)
                .SumAsync(x => (decimal?)(x.OnHandQuantity - x.ReservedQuantity)) ?? 0m;
            var held = await db.SalesInvoiceLines.AsNoTracking().Where(x => x.ItemId == itemId &&
                x.SalesInvoice.Status == SalesInvoiceStatus.Issued && x.SalesInvoice.SalesDeliveryId != delivery.Id &&
                x.SalesInvoice.SalesDelivery.Status == SalesDeliveryStatus.Draft &&
                x.SalesInvoice.SalesDelivery.FromLocationId == delivery.FromLocationId).SumAsync(x => (decimal?)x.Quantity) ?? 0m;
            if (physical - held < quantity)
                throw new InvalidOperationException($"Insufficient uncommitted finished stock for {item.Name}.");
        }
    }
}
