using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Models.Shared.Services;

public class QuotationService
{
    private readonly ApplicationDbContext _db;
    private readonly NumberSeriesService _numberSeries;
    private readonly UserManager<ApplicationUser> _userManager;

    public QuotationService(
        ApplicationDbContext db,
        NumberSeriesService numberSeries,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _numberSeries = numberSeries;
        _userManager = userManager;
    }

    /// <summary>
    /// One-step job-to-quotation flow: no separate request submission required.
    /// Existing associations are reused instead of creating duplicate quotes.
    /// </summary>
    public async Task<Quotation> CreateFromRequestAsync(long salesRequestId)
    {
        var existingFromLine = await _db.QuotationLines
            .Where(x => x.SourceRequestId == salesRequestId)
            .Select(x => x.Quotation)
            .FirstOrDefaultAsync();
        if (existingFromLine != null)
            return existingFromLine;

        // Preserve old quotations that still use only the primary request FK.
        var legacy = await _db.Quotations
            .FirstOrDefaultAsync(x => x.SalesRequestId == salesRequestId);
        if (legacy != null)
            return legacy;

        var request = await _db.SalesRequests
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == salesRequestId)
            ?? throw new InvalidOperationException("Sales request not found.");

        ValidateRequestCanBeQuoted(request);

        var quotation = new Quotation
        {
            QuotationNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.Quotation),
            SalesRequestId = request.Id, // backwards-compatible primary request
            CustomerId = request.CustomerId,
            Status = QuotationStatus.Draft,
            QuotationDate = DateTime.Now
        };

        foreach (var line in request.Lines)
        {
            quotation.Lines.Add(new QuotationLine
            {
                SourceRequestId = request.Id,
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                Notes = line.Notes,
                UnitPrice = 0
            });
        }

        _db.Quotations.Add(quotation);
        request.Status = SalesRequestStatus.QuotationCreated;
        await _db.SaveChangesAsync();
        return quotation;
    }

    /// <summary>
    /// Attach another saved Roll/Sheet request to an editable quotation.
    /// Multiple requests must belong to the SAME customer. Each line keeps
    /// its SourceRequestId, allowing separate customer approval proofs.
    /// </summary>
    public async Task AddRequestToQuotationAsync(long quotationId, long salesRequestId)
    {
        var quotation = await _db.Quotations
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == quotationId)
            ?? throw new InvalidOperationException("Quotation not found.");

        if (quotation.Status is not (QuotationStatus.Draft or QuotationStatus.RevisionRequired))
            throw new InvalidOperationException("Requests can only be attached to editable quotations.");

        if (quotation.SalesRequestId == salesRequestId ||
            quotation.Lines.Any(x => x.SourceRequestId == salesRequestId))
            return; // idempotent add

        var linkedElsewhere =
            await _db.Quotations.AnyAsync(x => x.SalesRequestId == salesRequestId) ||
            await _db.QuotationLines.AnyAsync(x => x.SourceRequestId == salesRequestId);
        if (linkedElsewhere)
            throw new InvalidOperationException("This request is already attached to another quotation.");

        var request = await _db.SalesRequests
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == salesRequestId)
            ?? throw new InvalidOperationException("Sales request not found.");

        if (request.CustomerId != quotation.CustomerId)
            throw new InvalidOperationException("All requests in one quotation must belong to the same customer.");

        ValidateRequestCanBeQuoted(request);

        foreach (var line in request.Lines)
        {
            quotation.Lines.Add(new QuotationLine
            {
                QuotationId = quotation.Id,
                SourceRequestId = request.Id,
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                Notes = line.Notes,
                UnitPrice = 0
            });
        }

        request.Status = SalesRequestStatus.QuotationCreated;
        quotation.FinalAmount = quotation.Lines.Sum(x => x.TotalPrice);
        await _db.SaveChangesAsync();
    }

    public async Task<List<SalesRequest>> GetAvailableRequestsAsync(int customerId)
    {
        return await _db.SalesRequests.AsNoTracking()
            .Where(r => r.CustomerId == customerId &&
                (r.Status == SalesRequestStatus.Draft ||
                 r.Status == SalesRequestStatus.Submitted ||
                 r.Status == SalesRequestStatus.EvaluationCompleted) &&
                r.Lines.Any() &&
                !(_db.Quotations.Any(q => q.SalesRequestId == r.Id) ||
                  _db.QuotationLines.Any(line => line.SourceRequestId == r.Id)))
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    private static void ValidateRequestCanBeQuoted(SalesRequest request)
    {
        if (request.Status is not (SalesRequestStatus.Draft or
            SalesRequestStatus.Submitted or SalesRequestStatus.EvaluationCompleted))
            throw new InvalidOperationException("This request is not available for quotation.");

        if (request.CustomerId <= 0 || request.Lines.Count == 0 ||
            request.Lines.Any(x => x.ItemId <= 0 || x.UnitId <= 0 || x.Quantity <= 0))
            throw new InvalidOperationException(
                "Save the job with its title, positive quantity and finished-product item before quoting.");
    }

    public async Task AssignPricingUserAsEvaluatorAsync(long quotationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException(
                "An authenticated user is required to assign the evaluator.");

        var quotation = await _db.Quotations
            .Include(x => x.SalesRequest)
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == quotationId);

        if (quotation == null)
            throw new InvalidOperationException("Quotation not found.");

        if (quotation.SalesRequest == null)
            throw new InvalidOperationException(
                "The sales request linked to this quotation was not found.");

        var hasPricing =
            quotation.Lines.Any(x => x.UnitPrice > 0) ||
            quotation.MaterialCost > 0 ||
            quotation.ProductionCost > 0;

        if (!hasPricing)
            return;

        // Apply the evaluator to every linked request, not just the primary.
        var requestIds = quotation.Lines
            .Where(line => line.SourceRequestId.HasValue)
            .Select(line => line.SourceRequestId!.Value)
            .Append(quotation.SalesRequestId)
            .Distinct()
            .ToArray();

        var requests = await _db.SalesRequests
            .Where(r => requestIds.Contains(r.Id))
            .ToListAsync();

        foreach (var request in requests)
            request.AssignedToUserId = userId;

        await _db.SaveChangesAsync();
    }

    public async Task SubmitForApprovalAsync(long quotationId)
    {
        var quotation = await _db.Quotations
            .FirstOrDefaultAsync(x => x.Id == quotationId);

        if (quotation == null)
            throw new InvalidOperationException("Quotation not found.");

        if (quotation.Status != QuotationStatus.Draft &&
            quotation.Status != QuotationStatus.RevisionRequired)
            throw new InvalidOperationException(
                "Only draft or revision-required quotations can be submitted for approval.");

        quotation.Status = QuotationStatus.PendingApproval;
        await _db.SaveChangesAsync();
    }

    public async Task ApproveAsync(long quotationId, string userId, string comment = "")
    {
        await EnsureAdministratorAsync(userId);

        var quotation = await _db.Quotations
            .FirstOrDefaultAsync(x => x.Id == quotationId);

        if (quotation == null)
            throw new InvalidOperationException("Quotation not found.");

        if (quotation.Status != QuotationStatus.PendingApproval)
            throw new InvalidOperationException(
                "Only pending quotations can be approved.");

        quotation.Status = QuotationStatus.Approved;

        _db.ApprovalHistories.Add(new ApprovalHistory
        {
            QuotationId = quotationId,
            UserId = userId,
            Approved = true,
            Comment = comment
        });

        await _db.SaveChangesAsync();
    }

    public async Task RejectAsync(long quotationId, string userId, string comment)
    {
        await EnsureAdministratorAsync(userId);

        var quotation = await _db.Quotations
            .FirstOrDefaultAsync(x => x.Id == quotationId);

        if (quotation == null)
            throw new InvalidOperationException("Quotation not found.");

        if (quotation.Status != QuotationStatus.PendingApproval)
            throw new InvalidOperationException(
                "Only pending quotations can be rejected.");

        quotation.Status = QuotationStatus.RevisionRequired;

        _db.ApprovalHistories.Add(new ApprovalHistory
        {
            QuotationId = quotationId,
            UserId = userId,
            Approved = false,
            Comment = comment
        });

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Records the customer's decision directly after internal approval.
    /// Legacy intermediate statuses remain accepted for existing quotations.
    /// </summary>
    public async Task CustomerDecisionAsync(long quotationId, bool accepted)
    {
        var quotation = await _db.Quotations
            .FirstOrDefaultAsync(x => x.Id == quotationId);

        if (quotation == null)
            throw new InvalidOperationException("Quotation not found.");

        var canRecordDecision =
            quotation.Status == QuotationStatus.Approved ||
            quotation.Status == QuotationStatus.ReadyToSend ||
            quotation.Status == QuotationStatus.SentToCustomer ||
            quotation.Status == QuotationStatus.CustomerNegotiation;

        if (!canRecordDecision)
            throw new InvalidOperationException(
                "A customer decision can only be recorded after the quotation has been approved.");

        // A customer rejection is a request to revise the price, not the end
        // of the quotation workflow. Return it to Draft so pricing can be
        // updated and submitted for internal approval again.
        quotation.Status = accepted
            ? QuotationStatus.Accepted
            : QuotationStatus.Draft;

        await _db.SaveChangesAsync();
    }

    private async Task EnsureAdministratorAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException(
                "An authenticated Administrator is required.");

        var user = await _userManager.FindByIdAsync(userId);

        if (user == null ||
            !await _userManager.IsInRoleAsync(user, "Administrator"))
            throw new UnauthorizedAccessException(
                "Only an Administrator can approve or reject quotations.");
    }
}
