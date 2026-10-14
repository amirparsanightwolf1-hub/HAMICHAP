using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using HamiChap_MES_ERP.Models.Production.Quality;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class QualityInspectionService
{
    private readonly ApplicationDbContext _db;

    public QualityInspectionService(
        ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<QualityInspection>
        CreateInspectionAsync(
            long productionOrderId,
            int itemId,
            decimal quantity)
    {
        var order =
            await _db.ProductionOrders
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

        _db.QualityInspections.Add(inspection);

        await _db.SaveChangesAsync();

        return inspection;
    }


    public async Task AddResultAsync(
        long inspectionId,
        string parameterName,
        decimal? measuredValue,
        decimal? minimumValue,
        decimal? maximumValue,
        string? textValue = null,
        string notes = "")
    {
        var inspection =
            await _db.QualityInspections
                .FirstOrDefaultAsync(x =>
                    x.Id == inspectionId);

        if (inspection == null)
            throw new Exception(
                "Quality inspection not found.");

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

        _db.QualityInspectionResults.Add(
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

        await _db.SaveChangesAsync();
    }
    public async Task CompleteInspectionAsync(
    long inspectionId,
    decimal acceptedQuantity,
    decimal rejectedQuantity,
    decimal reworkQuantity)
    {
        var inspection =
            await _db.QualityInspections
                .Include(x => x.Results)
                .FirstOrDefaultAsync(x =>
                    x.Id == inspectionId);

        if (inspection == null)
            throw new InvalidOperationException(
                "Quality inspection not found.");

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

        if (totalProcessed >
            inspection.InspectedQuantity)
        {
            throw new InvalidOperationException(
                "Accepted, rejected and rework quantities exceed inspected quantity.");
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

        await _db.SaveChangesAsync();
    }
}