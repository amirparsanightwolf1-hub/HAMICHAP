using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class InventoryReservationService
{
    private readonly ApplicationDbContext _db;

    public InventoryReservationService(
        ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> ReserveForProductionAsync(long productionOrderId)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var mrp = await _db.MaterialRequirementPlans
                .Include(x => x.Lines)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync(x =>
                    x.ProductionOrderId == productionOrderId);

            if (mrp == null)
                throw new InvalidOperationException(
                    "MRP plan not found.");

            if (mrp.Lines.Count == 0)
                throw new InvalidOperationException(
                    "MRP plan contains no material requirements.");

            var existingReservations =
                await _db.ProductionMaterialReservations
                    .Where(x =>
                        x.ProductionOrderId == productionOrderId &&
                        x.Status == ReservationStatus.Reserved)
                    .ToListAsync();

            // =====================================================
            // First pass: verify that everything can be reserved
            // =====================================================

            foreach (var line in mrp.Lines)
            {
                var alreadyReserved =
                    existingReservations
                        .Where(x => x.ItemId == line.ItemId)
                        .Sum(x => x.Quantity);

                var remainingRequired =
                    line.RequiredQuantity - alreadyReserved;

                if (remainingRequired <= 0)
                    continue;

                var available =
                    await _db.InventoryBalances
                        .Where(x =>
                            x.ItemId == line.ItemId &&
                            x.CustomerId == null)
                        .SumAsync(x =>
                            x.OnHandQuantity -
                            x.ReservedQuantity);

                if (available < remainingRequired)
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }

            // =====================================================
            // Second pass: reserve from actual inventory balances
            // =====================================================

            foreach (var line in mrp.Lines)
            {
                var alreadyReserved =
                    existingReservations
                        .Where(x => x.ItemId == line.ItemId)
                        .Sum(x => x.Quantity);

                var remainingRequired =
                    line.RequiredQuantity - alreadyReserved;

                if (remainingRequired <= 0)
                    continue;

                var balances =
                    await _db.InventoryBalances
                        .Include(x => x.InventoryLocation)
                        .Include(x => x.InventoryLot)
                        .Where(x =>
                            x.ItemId == line.ItemId &&
                            x.CustomerId == null &&
                            (x.OnHandQuantity -
                             x.ReservedQuantity) > 0)
                        .OrderBy(x => x.InventoryLocationId)
                        .ThenBy(x => x.InventoryLotId)
                        .ToListAsync();

                var remaining = remainingRequired;

                foreach (var balance in balances)
                {
                    var available =
                        balance.OnHandQuantity -
                        balance.ReservedQuantity;

                    if (available <= 0)
                        continue;

                    var reserveQuantity =
                        Math.Min(
                            available,
                            remaining);

                    balance.ReservedQuantity +=
                        reserveQuantity;

                    balance.LastUpdated =
                        DateTime.Now;

                    _db.ProductionMaterialReservations.Add(
                        new ProductionMaterialReservation
                        {
                            ProductionOrderId =
                                productionOrderId,

                            ItemId =
                                line.ItemId,

                            Quantity =
                                reserveQuantity,

                            UnitId =
                                line.UnitId,

                            InventoryLocationId =
                                balance.InventoryLocationId,

                            InventoryLotId =
                                balance.InventoryLotId,

                            ReservedDate =
                                DateTime.Now,

                            Status =
                                ReservationStatus.Reserved
                        });

                    remaining -= reserveQuantity;

                    if (remaining <= 0)
                        break;
                }

                if (remaining > 0)
                {
                    throw new InvalidOperationException(
                        $"Unable to reserve the required quantity for item {line.ItemId}.");
                }
            }

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}