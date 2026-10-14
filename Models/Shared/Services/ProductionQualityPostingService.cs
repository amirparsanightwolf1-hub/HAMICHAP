using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Production;
using HamiChap_MES_ERP.Models.Production.Quality;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class ProductionQualityPostingService
{
    private readonly ApplicationDbContext _db;
    private readonly InventoryPostingService _inventoryPosting;
    private readonly NumberSeriesService _numberSeries;

    public ProductionQualityPostingService(
        ApplicationDbContext db,
        InventoryPostingService inventoryPosting,
        NumberSeriesService numberSeries)
    {
        _db = db;
        _inventoryPosting = inventoryPosting;
        _numberSeries = numberSeries;
    }

    // =========================================================
    // Accepted quantity -> Finished Goods
    // =========================================================

    public async Task PostAcceptedQuantityAsync(
        long inspectionId,
        int productionLocationId,
        int finishedGoodsLocationId,
        string userId)
    {
        var inspection =
            await LoadInspectionAsync(inspectionId);

        if (inspection.AcceptedQuantity <= 0)
            return;

        await CreateAndPostDocumentAsync(
            inspection,
            InventoryDocumentType.ProductionReceipt,
            NumberSeriesType.ProductionReceipt,
            productionLocationId,
            finishedGoodsLocationId,
            inspection.AcceptedQuantity,
            "Accepted by Quality Control",
            userId);
    }

    // =========================================================
    // Rejected quantity -> Waste
    // =========================================================

    public async Task PostRejectedQuantityAsync(
        long inspectionId,
        int productionLocationId,
        int wasteLocationId,
        string userId)
    {
        var inspection =
            await LoadInspectionAsync(inspectionId);

        if (inspection.RejectedQuantity <= 0)
            return;

        await CreateAndPostDocumentAsync(
            inspection,
            InventoryDocumentType.ProductionScrap,
            NumberSeriesType.ProductionScrap,
            productionLocationId,
            wasteLocationId,
            inspection.RejectedQuantity,
            "Rejected by Quality Control",
            userId);
    }

    // =========================================================
    // Rework quantity -> Rework Area
    // =========================================================

    public async Task PostReworkQuantityAsync(
        long inspectionId,
        int productionLocationId,
        int reworkLocationId,
        string userId)
    {
        var inspection =
            await LoadInspectionAsync(inspectionId);

        if (inspection.ReworkQuantity <= 0)
            return;

        await CreateAndPostDocumentAsync(
            inspection,
            InventoryDocumentType.MaterialTransfer,
            NumberSeriesType.MaterialTransfer,
            productionLocationId,
            reworkLocationId,
            inspection.ReworkQuantity,
            "Sent to rework by Quality Control",
            userId);
    }

    // =========================================================
    // Post complete QC result
    // =========================================================

    public async Task PostQualityResultAsync(
        long inspectionId,
        int productionLocationId,
        int finishedGoodsLocationId,
        int wasteLocationId,
        int reworkLocationId,
        string userId)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var inspection =
                await LoadInspectionAsync(inspectionId);

            ValidateInspectionForPosting(inspection);

            if (inspection.AcceptedQuantity > 0)
            {
                await CreateAndPostDocumentAsync(
                    inspection,
                    InventoryDocumentType.ProductionReceipt,
                    NumberSeriesType.ProductionReceipt,
                    productionLocationId,
                    finishedGoodsLocationId,
                    inspection.AcceptedQuantity,
                    "Accepted by Quality Control",
                    userId,
                    useTransaction: false);
            }

            if (inspection.RejectedQuantity > 0)
            {
                await CreateAndPostDocumentAsync(
                    inspection,
                    InventoryDocumentType.ProductionScrap,
                    NumberSeriesType.ProductionScrap,
                    productionLocationId,
                    wasteLocationId,
                    inspection.RejectedQuantity,
                    "Rejected by Quality Control",
                    userId,
                    useTransaction: false);
            }

            if (inspection.ReworkQuantity > 0)
            {
                await CreateAndPostDocumentAsync(
                    inspection,
                    InventoryDocumentType.MaterialTransfer,
                    NumberSeriesType.MaterialTransfer,
                    productionLocationId,
                    reworkLocationId,
                    inspection.ReworkQuantity,
                    "Sent to rework by Quality Control",
                    userId,
                    useTransaction: false);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // =========================================================
    // Create and post one inventory document
    // =========================================================

    private async Task CreateAndPostDocumentAsync(
        QualityInspection inspection,
        InventoryDocumentType documentType,
        NumberSeriesType numberSeriesType,
        int fromLocationId,
        int toLocationId,
        decimal quantity,
        string notes,
        string userId,
        bool useTransaction = true)
    {
        if (quantity <= 0)
            return;

        var unitId =
            await GetItemUnitIdAsync(
                inspection.ItemId);

        var document = new InventoryDocument
        {
            DocumentNumber =
        await _numberSeries.GetNextNumberAsync(
            numberSeriesType),

            DocumentType =
        documentType,

            DocumentDate =
        inspection.InspectionDate,

            FromLocationId =
        fromLocationId,

            ToLocationId =
        toLocationId,

            ProductionOrderId =
        inspection.ProductionOrderId,

            QualityInspectionId =
        inspection.Id,

            Status =
        InventoryDocumentStatus.Draft,

            UserId =
        userId,

            Notes =
        $"QC inspection {inspection.Id}: {notes}"
        };

        document.Lines.Add(
            new InventoryDocumentLine
            {
                ItemId =
                    inspection.ItemId,

                Quantity =
                    quantity,

                UnitId =
                    unitId,

                Notes =
                    notes
            });

        _db.InventoryDocuments.Add(document);

        await _db.SaveChangesAsync();

        await _inventoryPosting.PostAsync(
            document.Id,
            userId,
            useTransaction);
    }

    // =========================================================
    // Validation
    // =========================================================

    private static void ValidateInspectionForPosting(
        QualityInspection inspection)
    {
        if (inspection.Status != QualityInspectionStatus.Passed &&
            inspection.Status != QualityInspectionStatus.PartiallyPassed &&
            inspection.Status != QualityInspectionStatus.Failed)
        {
            throw new InvalidOperationException(
                "The quality inspection has not been completed.");
        }

        var total =
            inspection.AcceptedQuantity +
            inspection.RejectedQuantity +
            inspection.ReworkQuantity;

        if (total != inspection.InspectedQuantity)
        {
            throw new InvalidOperationException(
                "Accepted, rejected and rework quantities must equal the inspected quantity.");
        }
    }

    // =========================================================
    // Load inspection
    // =========================================================

    private async Task<QualityInspection> LoadInspectionAsync(
        long inspectionId)
    {
        var inspection =
            await _db.QualityInspections
                .FirstOrDefaultAsync(x =>
                    x.Id == inspectionId);

        if (inspection == null)
            throw new InvalidOperationException(
                "Quality inspection not found.");

        return inspection;
    }

    // =========================================================
    // Item unit
    // =========================================================

    private async Task<int> GetItemUnitIdAsync(
        int itemId)
    {
        var unitId =
            await _db.Items
                .Where(x => x.Id == itemId)
                .Select(x => (int?)x.UnitId)
                .FirstOrDefaultAsync();

        if (unitId == null)
            throw new InvalidOperationException(
                "Item unit was not found.");

        return unitId.Value;
    }
}