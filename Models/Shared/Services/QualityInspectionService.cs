using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using HamiChap_MES_ERP.Models.Production.Quality;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HamiChap_MES_ERP.Services;

public sealed class QualityInspectionService(
    IDbContextFactory<ApplicationDbContext> factory, PermissionService permissions)
{
    public const string ViewPermission = "Production.Quality.View";
    public const string EditPermission = "Production.Quality.Edit";

    private async Task RequireEditAsync(ClaimsPrincipal user)
    {
        if (!await permissions.HasPermissionAsync(user, EditPermission))
            throw new UnauthorizedAccessException("Quality inspection editing permission is required.");
    }

    private static void RequireOpenInspection(QualityInspection inspection)
    {
        if (inspection.Status is not (QualityInspectionStatus.Pending or QualityInspectionStatus.InProgress))
            throw new InvalidOperationException("Only open quality inspections can be edited.");
    }

    public async Task<QualityInspection>
        CreateInspectionAsync(
            long productionOrderId,
            int itemId,
            decimal quantity, ClaimsPrincipal user)
    {
        await RequireEditAsync(user);
        await using var db = await factory.CreateDbContextAsync();
        if (quantity <= 0)
            throw new InvalidOperationException("Inspected quantity must be positive.");
        var order =
            await db.ProductionOrders
                .FirstOrDefaultAsync(x =>
                    x.Id == productionOrderId);

        if (order == null)
            throw new Exception(
                "Production order not found.");

        var inspection =
            new QualityInspection
            {
                ProductionOrderId =
                    productionOrderId,

                ItemId =
                    itemId,

                InspectedQuantity =
                    quantity,

                Status =
                    QualityInspectionStatus.Pending
            };

        db.QualityInspections.Add(inspection);

        await db.SaveChangesAsync();

        return inspection;
    }


    public async Task AddResultAsync(
        long inspectionId,
        string parameterName,
        decimal? measuredValue,
        decimal? minimumValue,
        decimal? maximumValue,
        string? textValue,
        string notes, ClaimsPrincipal user)
    {
        await RequireEditAsync(user);
        await using var db = await factory.CreateDbContextAsync();
        var inspection =
            await db.QualityInspections
                .FirstOrDefaultAsync(x =>
                    x.Id == inspectionId);

        if (inspection == null)
            throw new Exception(
                "Quality inspection not found.");

        RequireOpenInspection(inspection);

        bool passed;

        if (measuredValue.HasValue &&
            minimumValue.HasValue &&
            maximumValue.HasValue)
        {
            passed =
                measuredValue.Value >= minimumValue.Value &&
                measuredValue.Value <= maximumValue.Value;
        }
        else
        {
            passed = true;
        }

        inspection.Status =
            QualityInspectionStatus.InProgress;

        db.QualityInspectionResults.Add(
            new QualityInspectionResult
            {
                QualityInspectionId =
                    inspectionId,

                ParameterName =
                    parameterName,

                MeasuredValue =
                    measuredValue,

                MinimumValue =
                    minimumValue,

                MaximumValue =
                    maximumValue,

                TextValue =
                    textValue,

                Passed =
                    passed,

                Notes =
                    notes
            });

        await db.SaveChangesAsync();
    }
    public async Task CompleteInspectionAsync(
    long inspectionId,
    decimal acceptedQuantity,
    decimal rejectedQuantity,
    decimal reworkQuantity, ClaimsPrincipal user)
    {
        await RequireEditAsync(user);
        await using var db = await factory.CreateDbContextAsync();
        var inspection =
            await db.QualityInspections
                .Include(x => x.Results)
                .FirstOrDefaultAsync(x =>
                    x.Id == inspectionId);

        if (inspection == null)
            throw new InvalidOperationException(
                "Quality inspection not found.");

        RequireOpenInspection(inspection);

        if (acceptedQuantity < 0 ||
            rejectedQuantity < 0 ||
            reworkQuantity < 0)
        {
            throw new InvalidOperationException(
                "Quantities cannot be negative.");
        }

        var totalProcessed =
            acceptedQuantity +
            rejectedQuantity +
            reworkQuantity;

        if (totalProcessed !=
            inspection.InspectedQuantity)
        {
            throw new InvalidOperationException(
                "Accepted, rejected and rework quantities must equal the inspected quantity.");
        }

        inspection.AcceptedQuantity =
            acceptedQuantity;

        inspection.RejectedQuantity =
            rejectedQuantity;

        inspection.ReworkQuantity =
            reworkQuantity;

        var allPassed =
            inspection.Results.Count > 0 &&
            inspection.Results.All(x => x.Passed);

        if (rejectedQuantity == 0 &&
            reworkQuantity == 0 &&
            acceptedQuantity == inspection.InspectedQuantity &&
            allPassed)
        {
            inspection.Status =
                QualityInspectionStatus.Passed;
        }
        else if (reworkQuantity > 0 &&
                 rejectedQuantity == 0)
        {
            inspection.Status =
                QualityInspectionStatus.PartiallyPassed;
        }
        else if (acceptedQuantity > 0)
        {
            inspection.Status =
                QualityInspectionStatus.PartiallyPassed;
        }
        else
        {
            inspection.Status =
                QualityInspectionStatus.Failed;
        }

        await db.SaveChangesAsync();
    }
}