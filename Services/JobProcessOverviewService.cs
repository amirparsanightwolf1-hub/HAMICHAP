using System.Text.Json;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using HamiChap_MES_ERP.Models.Sales.Enums;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

/// <summary>
/// Read-only, job-first production overview. Inferred routing is deliberately
/// distinguished from verified operation progress: ProductionOperation rows
/// belong to a production ORDER and currently have no SalesRequestId.
/// </summary>
public static class JobProcessOverviewService
{
    public enum StageState
    {
        NotScheduled,
        Untracked,
        Pending,
        Ready,
        Running,
        Completed,
        Cancelled
    }

    public sealed record ProcessStage(
        int Sequence,
        string Name,
        StageState State,
        string? Machine,
        DateTime? PlannedStart,
        DateTime? PlannedEnd,
        bool NeedsReview);

    public sealed class JobRow
    {
        public long RequestId { get; init; }
        public string RequestNumber { get; init; } = "";
        public string Title { get; init; } = "";
        public string Customer { get; init; } = "";
        public SalesJobType JobType { get; init; }
        public SalesRequestStatus RequestStatus { get; init; }
        public DateTime RequestDate { get; init; }
        public decimal? Quantity { get; init; }
        public long? QuotationId { get; init; }
        public long? SalesOrderId { get; init; }
        public string? SalesOrderNumber { get; init; }
        public long? ProductionOrderId { get; init; }
        public string? ProductionOrderNumber { get; init; }
        public ProductionOrderStatus? ProductionStatus { get; init; }
        public DateTime? PlannedEnd { get; init; }
        public IReadOnlyList<ProcessStage> Stages { get; init; } = Array.Empty<ProcessStage>();
        public bool SharedProductionRoute { get; init; }
        public bool InvalidSpecifications { get; init; }
        public bool UsingRecordedOperations { get; init; }
        /// <summary>Verified stage completion percentage, or null if operations
        /// cannot be attributed safely to this individual job.</summary>
        public int? JobProgressPercent { get; init; }
        /// <summary>Production-order-wide progress (never presented as per-job).</summary>
        public int? SharedOrderProgressPercent { get; init; }
        public int CompletedStages => Stages.Count(x => x.State == StageState.Completed);
        public int ActiveStages => Stages.Count(x => x.State != StageState.Cancelled);
        public string EditUrl => $"/sales/requests/{(JobType == SalesJobType.Roll ? "roll" : "sheet")}/edit/{RequestId}";
        public string ProofUrl => $"/sales/requests/proof/{RequestId}";
    }

    public static async Task<IReadOnlyList<JobRow>> LoadAsync(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        string? search = null,
        SalesJobType? jobType = null,
        long? requestId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.SalesRequests.AsNoTracking()
            .Include(r => r.Customer).Include(r => r.Lines)
            .AsQueryable();
        if (requestId.HasValue)
        {
            query = query.Where(r => r.Id == requestId.Value);
        }
        else
        {
            if (jobType.HasValue)
                query = query.Where(r => r.JobType == jobType.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(r =>
                    r.RequestNumber.Contains(term) ||
                    r.Customer.Name.Contains(term) ||
                    r.JobSpecificationJson.Contains(term));
            }
        }

        // Limit initial display and search results; searches run in SQL so an
        // older job remains discoverable by number, customer or title.
        var requests = await query
            .OrderByDescending(r => r.RequestDate).ThenByDescending(r => r.Id)
            .Take(requestId.HasValue ? 1 : 150).ToListAsync(cancellationToken);
        if (requests.Count == 0)
            return Array.Empty<JobRow>();

        var requestIds = requests.Select(r => r.Id).ToArray();
        var referenced = await db.QuotationLines.AsNoTracking()
            .Where(l => l.SourceRequestId.HasValue &&
                        requestIds.Contains(l.SourceRequestId.Value))
            .Select(l => new { l.QuotationId, l.SourceRequestId })
            .ToListAsync(cancellationToken);
        var linkedQuotationIds = referenced.Select(l => l.QuotationId).Distinct().ToArray();

        var quotations = await db.Quotations.AsNoTracking()
            .Where(q => requestIds.Contains(q.SalesRequestId) ||
                        linkedQuotationIds.Contains(q.Id))
            .ToListAsync(cancellationToken);
        var quotationIds = quotations.Select(q => q.Id).ToArray();

        // Include every source job for a quote when determining whether its
        // production operations can be attributed to a single job.
        var quoteSources = await db.QuotationLines.AsNoTracking()
            .Where(l => quotationIds.Contains(l.QuotationId) &&
                        l.SourceRequestId.HasValue)
            .Select(l => new { l.QuotationId, l.SourceRequestId })
            .ToListAsync(cancellationToken);
        var sourcesPerQuote = quotations.ToDictionary(
            q => q.Id,
            q => quoteSources
                .Where(l => l.QuotationId == q.Id)
                .Select(l => l.SourceRequestId!.Value)
                .Append(q.SalesRequestId).Distinct().Count());

        var salesOrders = await db.SalesOrders.AsNoTracking()
            .Where(o => quotationIds.Contains(o.QuotationId))
            .ToListAsync(cancellationToken);
        var salesOrderIds = salesOrders.Select(x => x.Id).ToArray();
        var productionOrders = await db.ProductionOrders.AsNoTracking()
            .Where(p => salesOrderIds.Contains(p.SalesOrderId))
            .ToListAsync(cancellationToken);
        var productionOrderIds = productionOrders.Select(p => p.Id).ToArray();

        var operations = await db.ProductionOperations.AsNoTracking()
            .Include(o => o.WorkCenter)
            .Where(o => productionOrderIds.Contains(o.ProductionOrderId))
            .OrderBy(o => o.Sequence).ThenBy(o => o.Id)
            .ToListAsync(cancellationToken);
        var operationIds = operations.Select(o => o.Id).ToArray();
        var schedules = await db.ProductionSchedules.AsNoTracking()
            .Include(s => s.Machine)
            .Where(s => operationIds.Contains(s.ProductionOperationId))
            .ToListAsync(cancellationToken);

        var operationsByOrder = operations.ToLookup(o => o.ProductionOrderId);
        var schedulesByOperation = schedules.ToLookup(s => s.ProductionOperationId);
        var rows = new List<JobRow>(requests.Count);

        foreach (var request in requests)
        {
            var quote = quotations
                .Where(q => q.SalesRequestId == request.Id ||
                            referenced.Any(l => l.QuotationId == q.Id &&
                                                l.SourceRequestId == request.Id))
                .OrderByDescending(q => q.Id)
                .FirstOrDefault();
            var salesOrder = salesOrders.Where(x => x.QuotationId == quote?.Id)
                .OrderByDescending(x => x.Id).FirstOrDefault();
            var productionOrder = productionOrders
                .Where(x => x.SalesOrderId == salesOrder?.Id)
                .OrderBy(x => x.Status == ProductionOrderStatus.Cancelled)
                .ThenByDescending(x => x.CreatedDate)
                .FirstOrDefault();

            var recorded = productionOrder is null
                ? new List<ProductionOperation>()
                : operationsByOrder[productionOrder.Id]
                    .OrderBy(x => x.Sequence).ThenBy(x => x.Id).ToList();

            var shared = productionOrder is not null && quote is not null &&
                sourcesPerQuote.GetValueOrDefault(quote.Id) > 1;

            IReadOnlyList<ProductionJobRouting.SuggestedStage> proposed;
            var invalidSpec = false;
            try
            {
                proposed = ProductionJobRouting.FromJob(
                    request.JobType, request.JobSpecificationJson);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or
                                       NotSupportedException)
            {
                proposed = Array.Empty<ProductionJobRouting.SuggestedStage>();
                invalidSpec = true;
            }

            var hasVerifiedRoute = recorded.Count > 0 && !shared;
            List<ProcessStage> stages;
            int? jobProgress;

            if (hasVerifiedRoute)
            {
                // Manager-edited production steps override sales suggestions
                // only when there is exactly one originating request.
                stages = recorded.Select(op =>
                {
                    var schedule = schedulesByOperation[op.Id]
                        .OrderByDescending(x => x.PlannedStart).FirstOrDefault();
                    return new ProcessStage(
                        op.Sequence,
                        op.Name,
                        FromStatus(op.Status),
                        schedule?.Machine?.Name ?? op.WorkCenter?.Name,
                        schedule?.PlannedStart,
                        schedule?.PlannedEnd,
                        op.Name.StartsWith("REVIEW |", StringComparison.OrdinalIgnoreCase));
                }).ToList();
                jobProgress = CalculateProgress(recorded);
            }
            else
            {
                stages = proposed.Select((stage, index) =>
                    new ProcessStage(index + 1, stage.Name,
                        shared ? StageState.Untracked : StageState.NotScheduled,
                        null, null, null,
                        stage.Name.StartsWith("REVIEW |", StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                // 0% means the required stages are known but no job-specific
                // operation completion has been recorded yet.
                jobProgress = shared ? null : 0;
            }

            var sharedProgress = shared && recorded.Count > 0
                ? CalculateProgress(recorded) : null;

            rows.Add(new JobRow
            {
                RequestId = request.Id,
                RequestNumber = request.RequestNumber,
                Title = ExtractTitle(request.JobSpecificationJson, request.RequestNumber),
                Customer = request.Customer?.Name ?? "",
                JobType = request.JobType,
                RequestStatus = request.Status,
                RequestDate = request.RequestDate,
                Quantity = request.Lines.FirstOrDefault()?.Quantity,
                QuotationId = quote?.Id,
                SalesOrderId = salesOrder?.Id,
                SalesOrderNumber = salesOrder?.OrderNumber,
                ProductionOrderId = productionOrder?.Id,
                ProductionOrderNumber = productionOrder?.OrderNumber,
                ProductionStatus = productionOrder?.Status,
                PlannedEnd = productionOrder?.PlannedEndDate,
                Stages = stages,
                SharedProductionRoute = shared,
                InvalidSpecifications = invalidSpec,
                UsingRecordedOperations = hasVerifiedRoute,
                JobProgressPercent = jobProgress,
                SharedOrderProgressPercent = sharedProgress
            });
        }

        return rows;
    }

    private static StageState FromStatus(ProductionOperationStatus status) =>
        status switch
        {
            ProductionOperationStatus.Ready => StageState.Ready,
            ProductionOperationStatus.Running => StageState.Running,
            ProductionOperationStatus.Completed => StageState.Completed,
            ProductionOperationStatus.Cancelled => StageState.Cancelled,
            _ => StageState.Pending
        };

    private static int CalculateProgress(IReadOnlyCollection<ProductionOperation> operations)
    {
        var active = operations.Count(x => x.Status != ProductionOperationStatus.Cancelled);
        if (active == 0)
            return 0;
        var finished = operations.Count(x => x.Status == ProductionOperationStatus.Completed);
        return (int)decimal.Round(finished * 100m / active, 0);
    }

    private static string ExtractTitle(string? json, string fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
            return fallback;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("JobTitle", out var title) &&
                title.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(title.GetString()))
                return title.GetString()!.Trim();
        }
        catch (JsonException) { }
        return fallback;
    }
}
