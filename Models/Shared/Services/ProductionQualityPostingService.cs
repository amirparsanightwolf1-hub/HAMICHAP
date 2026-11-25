using System.Security.Claims;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Production;
using HamiChap_MES_ERP.Models.Production.Quality;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public sealed class ProductionQualityPostingService(
    IDbContextFactory<ApplicationDbContext> factory, PermissionService permissions)
{
    public const string PostPermission = "Production.Quality.Post";

    public async Task PostQualityResultAsync(
        long inspectionId, int productionLocationId, int finishedGoodsLocationId,
        int wasteLocationId, int reworkLocationId, ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || !await permissions.HasPermissionAsync(user, PostPermission))
            throw new UnauthorizedAccessException("Quality inventory posting permission is required.");

        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Serialize all posting attempts for this inspection until stock and documents commit.
        var inspection = await db.QualityInspections
            .FromSqlInterpolated($"""
                SELECT * FROM [QualityInspections] WITH (UPDLOCK, HOLDLOCK)
                WHERE [Id] = {inspectionId}
                """)
            .SingleOrDefaultAsync() ?? throw new InvalidOperationException("Quality inspection not found.");

        if (await db.InventoryDocuments.AnyAsync(d =>
            d.QualityInspectionId == inspectionId && d.Status == InventoryDocumentStatus.Posted))
            return;

        ValidateInspectionForPosting(inspection);
        var unitId = await db.Items.Where(x => x.Id == inspection.ItemId).Select(x => x.UnitId).SingleAsync();
        var numbering = new NumberSeriesService(db);
        var posting = new InventoryPostingService(db);

        var movements = new[]
        {
            (inspection.AcceptedQuantity, finishedGoodsLocationId, InventoryDocumentType.ProductionReceipt,
                NumberSeriesType.ProductionReceipt, "Accepted by Quality Control"),
            (inspection.RejectedQuantity, wasteLocationId, InventoryDocumentType.ProductionScrap,
                NumberSeriesType.ProductionScrap, "Rejected by Quality Control"),
            (inspection.ReworkQuantity, reworkLocationId, InventoryDocumentType.MaterialTransfer,
                NumberSeriesType.MaterialTransfer, "Sent to rework by Quality Control")
        };
        foreach (var (quantity, destination, type, series, notes) in movements)
        {
            if (quantity == 0)
                continue;

            var document = new InventoryDocument
            {
                DocumentNumber = await numbering.GetNextNumberAsync(series),
                DocumentType = type,
                DocumentDate = inspection.InspectionDate,
                FromLocationId = productionLocationId,
                ToLocationId = destination,
                ProductionOrderId = inspection.ProductionOrderId,
                QualityInspectionId = inspection.Id,
                UserId = userId,
                Notes = $"QC inspection {inspection.Id}: {notes}",
                Lines = [new InventoryDocumentLine { ItemId = inspection.ItemId, Quantity = quantity, UnitId = unitId, Notes = notes }]
            };
            db.InventoryDocuments.Add(document);
            await db.SaveChangesAsync();
            await posting.PostAsync(document.Id, userId, useTransaction: false);
        }
        await transaction.CommitAsync();
    }

    private static void ValidateInspectionForPosting(QualityInspection inspection)
    {
        if (inspection.Status is not (QualityInspectionStatus.Passed or QualityInspectionStatus.PartiallyPassed or QualityInspectionStatus.Failed))
            throw new InvalidOperationException("The quality inspection has not been completed.");

        if (inspection.InspectedQuantity <= 0 || inspection.AcceptedQuantity < 0 ||
            inspection.RejectedQuantity < 0 || inspection.ReworkQuantity < 0 ||
            inspection.AcceptedQuantity + inspection.RejectedQuantity + inspection.ReworkQuantity != inspection.InspectedQuantity)
            throw new InvalidOperationException("Accepted, rejected and rework quantities must equal the inspected quantity.");
    }
}
