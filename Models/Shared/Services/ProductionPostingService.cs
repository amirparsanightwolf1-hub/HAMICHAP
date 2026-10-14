using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Production;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class ProductionPostingService
{
    private readonly ApplicationDbContext _db;
    private readonly InventoryPostingService _inventoryPosting;
    private readonly NumberSeriesService _numberSeries;

    public ProductionPostingService(
        ApplicationDbContext db,
        InventoryPostingService inventoryPosting,
        NumberSeriesService numberSeries)
    {
        _db = db;
        _inventoryPosting = inventoryPosting;
        _numberSeries = numberSeries;
    }

    public async Task<List<long>> PostMaterialIssueAsync(
        long productionOrderId,
        int toLocationId,
        string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException(
                "A valid user is required.");

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var order = await _db.ProductionOrders
                .FirstOrDefaultAsync(x =>
                    x.Id == productionOrderId);

            if (order == null)
                throw new InvalidOperationException(
                    "Production order not found.");

            if (order.Status != ProductionOrderStatus.ReadyToStart)
                throw new InvalidOperationException(
                    "Materials can only be issued when the production order is Ready To Start.");

            var reservations =
                await _db.ProductionMaterialReservations
                    .Include(x => x.Item)
                    .Include(x => x.InventoryLot)
                    .Where(x =>
                        x.ProductionOrderId == productionOrderId &&
                        x.Status == ReservationStatus.Reserved &&
                        x.Quantity > 0)
                    .OrderBy(x => x.InventoryLocationId)
                    .ThenBy(x => x.InventoryLotId)
                    .ThenBy(x => x.ItemId)
                    .ToListAsync();

            if (reservations.Count == 0)
                throw new InvalidOperationException(
                    "No reserved materials are available for issue.");

            var destination =
                await _db.InventoryLocations
                    .FirstOrDefaultAsync(x =>
                        x.Id == toLocationId &&
                        x.IsActive);

            if (destination == null)
                throw new InvalidOperationException(
                    "Production destination location was not found or is inactive.");

            var sourceLocationIds =
                reservations
                    .Select(x => x.InventoryLocationId)
                    .Distinct()
                    .ToList();

            var sourceLocations =
                await _db.InventoryLocations
                    .Where(x =>
                        sourceLocationIds.Contains(x.Id) &&
                        x.IsActive)
                    .ToDictionaryAsync(x => x.Id);

            if (sourceLocations.Count != sourceLocationIds.Count)
                throw new InvalidOperationException(
                    "One or more reservation source locations were not found or are inactive.");

            var documentIds = new List<long>();

            // =====================================================
            // Validate all reserved stock before changing anything
            // =====================================================

            foreach (var reservation in reservations)
            {
                var balance =
                    await _db.InventoryBalances
                        .FirstOrDefaultAsync(x =>
                            x.ItemId == reservation.ItemId &&
                            x.InventoryLocationId ==
                                reservation.InventoryLocationId &&
                            x.InventoryLotId ==
                                reservation.InventoryLotId &&
                            x.CustomerId == null);

                if (balance == null)
                {
                    throw new InvalidOperationException(
                        $"Reserved inventory balance was not found for item '{reservation.Item?.Name}'.");
                }

                if (balance.ReservedQuantity <
                    reservation.Quantity)
                {
                    throw new InvalidOperationException(
                        $"Reserved quantity is invalid for item '{reservation.Item?.Name}'.");
                }

                if (balance.OnHandQuantity <
                    reservation.Quantity)
                {
                    throw new InvalidOperationException(
                        $"On-hand quantity is insufficient for item '{reservation.Item?.Name}'.");
                }
            }

            // =====================================================
            // One Production Issue document per source location
            // =====================================================

            foreach (var group in reservations.GroupBy(
                x => x.InventoryLocationId))
            {
                var fromLocationId =
                    group.Key;

                var document =
                    new InventoryDocument
                    {
                        DocumentNumber =
                            await _numberSeries.GetNextNumberAsync(
                                NumberSeriesType.ProductionIssue,
                                useTransaction: false),

                        DocumentType =
                            InventoryDocumentType.ProductionIssue,

                        DocumentDate =
                            DateTime.Now,

                        FromLocationId =
                            fromLocationId,

                        ToLocationId =
                            toLocationId,

                        ProductionOrderId =
                            productionOrderId,

                        Status =
                            InventoryDocumentStatus.Draft,

                        Notes =
                            $"Material issue for Production Order {order.OrderNumber}"
                    };

                foreach (var reservation in group)
                {
                    var lotNumber =
                        reservation.InventoryLot?.LotNumber;

                    if (reservation.Item!.RequiresLotTracking &&
                        string.IsNullOrWhiteSpace(lotNumber))
                    {
                        throw new InvalidOperationException(
                            $"Lot number is required for reserved item '{reservation.Item.Name}'.");
                    }

                    document.Lines.Add(
                        new InventoryDocumentLine
                        {
                            ItemId =
                                reservation.ItemId,

                            Quantity =
                                reservation.Quantity,

                            UnitId =
                                reservation.UnitId,

                            LotNumber =
                                lotNumber,

                            InventoryLotId =
                                reservation.InventoryLotId,

                            CustomerId =
                                null,

                            Notes =
                                $"Production material issue for {order.OrderNumber}"
                        });
                }

                _db.InventoryDocuments.Add(document);

                await _db.SaveChangesAsync();

                // Release the reserved quantity immediately before
                // posting so InventoryPostingService can consume
                // the stock as available inventory.
                foreach (var reservation in group)
                {
                    var balance =
                        await _db.InventoryBalances
                            .FirstAsync(x =>
                                x.ItemId ==
                                    reservation.ItemId &&
                                x.InventoryLocationId ==
                                    reservation.InventoryLocationId &&
                                x.InventoryLotId ==
                                    reservation.InventoryLotId &&
                                x.CustomerId == null);

                    balance.ReservedQuantity -=
                        reservation.Quantity;

                    balance.LastUpdated =
                        DateTime.Now;
                }

                await _db.SaveChangesAsync();

                await _inventoryPosting.PostAsync(
                    document.Id,
                    userId,
                    useTransaction: false);

                foreach (var reservation in group)
                {
                    _db.ProductionMaterialConsumptions.Add(
                        new ProductionMaterialConsumption
                        {
                            ProductionOrderId =
                                productionOrderId,

                            ItemId =
                                reservation.ItemId,

                            Quantity =
                                reservation.Quantity,

                            UnitId =
                                reservation.UnitId,

                            ConsumptionDate =
                                DateTime.Now,

                            InventoryDocumentId =
                                document.Id
                        });

                    reservation.Quantity = 0;

                    reservation.Status =
                        ReservationStatus.Consumed;
                }

                await _db.SaveChangesAsync();

                documentIds.Add(document.Id);
            }

            order.Status =
                ProductionOrderStatus.InProduction;

            order.ActualStartDate ??=
                DateTime.Now;

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return documentIds;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}