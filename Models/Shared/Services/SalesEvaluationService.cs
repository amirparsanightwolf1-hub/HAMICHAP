using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Models.Shared.Services;

public class SalesEvaluationService
{
    private readonly ApplicationDbContext _db;

    public SalesEvaluationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SalesEvaluation> StartAsync(
        long salesRequestId,
        string evaluatorUserId)
    {
        var request = await _db.SalesRequests
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == salesRequestId);

        if (request == null)
            throw new InvalidOperationException(
                "Sales request not found.");

        if (request.Status != SalesRequestStatus.Submitted &&
    request.Status != SalesRequestStatus.UnderEvaluation)
        {
            throw new InvalidOperationException(
                "Sales request must be submitted before evaluation.");
        }

        var existingEvaluation = await _db.SalesEvaluations
            .AnyAsync(x =>
                x.SalesRequestId == salesRequestId &&
                x.Status != SalesEvaluationStatus.Cancelled);

        if (existingEvaluation)
            throw new InvalidOperationException(
                "An active evaluation already exists for this sales request.");

        var evaluation = new SalesEvaluation
        {
            SalesRequestId = salesRequestId,
            EvaluatorUserId = evaluatorUserId,
            Status = SalesEvaluationStatus.InProgress
        };

        foreach (var line in request.Lines)
        {
            var available = await _db.InventoryBalances
                .Where(x =>
                    x.ItemId == line.ItemId &&
                    x.CustomerId == null)
                .SumAsync(x =>
                    x.OnHandQuantity -
                    x.ReservedQuantity);

            evaluation.Lines.Add(
                new SalesEvaluationLine
                {
                    ItemId = line.ItemId,
                    RequiredQuantity = line.Quantity,
                    AvailableQuantity = available
                });
        }

        _db.SalesEvaluations.Add(evaluation);

        request.Status = SalesRequestStatus.UnderEvaluation;

        await _db.SaveChangesAsync();

        return evaluation;
    }

    public async Task CompleteAsync(long evaluationId)
    {
        var evaluation = await _db.SalesEvaluations
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == evaluationId);

        if (evaluation == null)
            throw new InvalidOperationException(
                "Sales evaluation not found.");

        if (evaluation.Status != SalesEvaluationStatus.InProgress)
            throw new InvalidOperationException(
                "Sales evaluation is not in progress.");

        var request = await _db.SalesRequests
            .FirstOrDefaultAsync(x =>
                x.Id == evaluation.SalesRequestId);

        if (request == null)
            throw new InvalidOperationException(
                "Sales request not found.");

        evaluation.Status = SalesEvaluationStatus.Completed;

        request.Status = SalesRequestStatus.EvaluationCompleted;

        await _db.SaveChangesAsync();
    }
}