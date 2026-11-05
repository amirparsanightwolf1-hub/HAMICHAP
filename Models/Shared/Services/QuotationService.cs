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

    public async Task<Quotation> CreateFromRequestAsync(
        long salesRequestId)
    {
        var request = await _db.SalesRequests
            .Include(x => x.Lines)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == salesRequestId);

        if (request == null)
            throw new InvalidOperationException("Sales request not found.");

        if (request.Status != SalesRequestStatus.EvaluationCompleted)
            throw new InvalidOperationException(
                "Request evaluation is not completed.");

        var number = await _numberSeries.GetNextNumberAsync(
            NumberSeriesType.Quotation);

        var quotation = new Quotation
        {
            QuotationNumber = number,
            SalesRequestId = request.Id,
            CustomerId = request.CustomerId,
            Status = QuotationStatus.Draft,
            QuotationDate = DateTime.Now
        };

        foreach (var line in request.Lines)
        {
            quotation.Lines.Add(new QuotationLine
            {
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                UnitPrice = 0
            });
        }

        _db.Quotations.Add(quotation);
        request.Status = SalesRequestStatus.QuotationCreated;

        await _db.SaveChangesAsync();

        return quotation;
    }

    public async Task SubmitForApprovalAsync(long quotationId)
    {
        var quotation = await _db.Quotations
            .FirstOrDefaultAsync(x => x.Id == quotationId);

        if (quotation == null)
            throw new InvalidOperationException("Quotation not found.");

        if (quotation.Status != QuotationStatus.Draft &&
            quotation.Status != QuotationStatus.RevisionRequired)
        {
            throw new InvalidOperationException(
                "Only draft or revision-required quotations can be submitted for approval.");
        }

        quotation.Status = QuotationStatus.PendingApproval;

        await _db.SaveChangesAsync();
    }

    public async Task ApproveAsync(
        long quotationId,
        string userId,
        string comment = "")
    {
        await EnsureAdministratorAsync(userId);

        var quotation = await _db.Quotations
            .FirstOrDefaultAsync(x => x.Id == quotationId);

        if (quotation == null)
            throw new InvalidOperationException("Quotation not found.");

        if (quotation.Status != QuotationStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                "Only pending quotations can be approved.");
        }

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

    public async Task RejectAsync(
        long quotationId,
        string userId,
        string comment)
    {
        await EnsureAdministratorAsync(userId);

        var quotation = await _db.Quotations
            .FirstOrDefaultAsync(x => x.Id == quotationId);

        if (quotation == null)
            throw new InvalidOperationException("Quotation not found.");

        if (quotation.Status != QuotationStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                "Only pending quotations can be rejected.");
        }

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

    public async Task MarkReadyToSendAsync(long quotationId)
    {
        var quotation = await _db.Quotations
            .FirstOrDefaultAsync(x => x.Id == quotationId);

        if (quotation == null)
            throw new InvalidOperationException("Quotation not found.");

        if (quotation.Status != QuotationStatus.Approved)
        {
            throw new InvalidOperationException(
                "Quotation must be approved before it can be marked ready to send.");
        }

        quotation.Status = QuotationStatus.ReadyToSend;

        await _db.SaveChangesAsync();
    }

    public async Task SendToCustomerAsync(long quotationId)
    {
        var quotation = await _db.Quotations
            .FirstOrDefaultAsync(x => x.Id == quotationId);

        if (quotation == null)
            throw new InvalidOperationException("Quotation not found.");

        if (quotation.Status != QuotationStatus.Approved &&
            quotation.Status != QuotationStatus.ReadyToSend)
        {
            throw new InvalidOperationException(
                "Only approved quotations can be sent to the customer.");
        }

        quotation.Status = QuotationStatus.SentToCustomer;

        await _db.SaveChangesAsync();
    }

    public async Task CustomerDecisionAsync(
        long quotationId,
        bool accepted)
    {
        var quotation = await _db.Quotations
            .FirstOrDefaultAsync(x => x.Id == quotationId);

        if (quotation == null)
            throw new InvalidOperationException("Quotation not found.");

        if (quotation.Status != QuotationStatus.SentToCustomer &&
            quotation.Status != QuotationStatus.CustomerNegotiation)
        {
            throw new InvalidOperationException(
                "A customer decision can only be recorded after the quotation has been sent.");
        }

        // A customer rejection returns the quotation to Draft so it can be edited
        // and submitted through the approval workflow again.
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
        {
            throw new UnauthorizedAccessException(
                "Only an Administrator can approve or reject quotations.");
        }
    }
}