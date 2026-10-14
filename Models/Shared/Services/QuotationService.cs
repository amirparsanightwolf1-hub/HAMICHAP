using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Models.Shared.Services;

public class QuotationService
{
    private readonly ApplicationDbContext _db;
    private readonly NumberSeriesService _numberSeries;


    public QuotationService(
        ApplicationDbContext db,
        NumberSeriesService numberSeries)
    {
        _db = db;
        _numberSeries = numberSeries;
    }



    public async Task<Quotation> CreateFromRequestAsync(
        long salesRequestId)
    {

        var request =
            await _db.SalesRequests
            .Include(x => x.Lines)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x =>
                x.Id == salesRequestId);



        if (request == null)
            throw new Exception(
                "Sales request not found");



        if (request.Status !=
            SalesRequestStatus.EvaluationCompleted)
        {
            throw new Exception(
                "Request evaluation is not completed");
        }



        var number =
            await _numberSeries
                .GetNextNumberAsync(
                    NumberSeriesType.Quotation);



        var quotation =
            new Quotation
            {
                QuotationNumber = number,

                SalesRequestId =
                    request.Id,

                CustomerId =
                    request.CustomerId,

                Status =
                    QuotationStatus.Draft,

                QuotationDate =
                    DateTime.Now
            };



        foreach (var line in request.Lines)
        {
            quotation.Lines.Add(
                new QuotationLine
                {
                    ItemId = line.ItemId,

                    Quantity = line.Quantity,

                    UnitPrice = 0
                });
        }



        _db.Quotations.Add(quotation);


        request.Status =
            SalesRequestStatus.QuotationCreated;


        await _db.SaveChangesAsync();


        return quotation;
    }




    public async Task SubmitForApprovalAsync(
        long quotationId)
    {

        var quotation =
            await _db.Quotations
            .FirstAsync(x =>
                x.Id == quotationId);



        quotation.Status =
            QuotationStatus.PendingApproval;



        await _db.SaveChangesAsync();
    }





    public async Task ApproveAsync(
        long quotationId,
        string userId,
        string comment = "")
    {

        var quotation =
            await _db.Quotations
            .FirstAsync(x =>
                x.Id == quotationId);



        quotation.Status =
            QuotationStatus.Approved;



        _db.ApprovalHistories.Add(
            new ApprovalHistory
            {
                QuotationId =
                    quotationId,

                UserId =
                    userId,

                Approved =
                    true,

                Comment =
                    comment
            });



        await _db.SaveChangesAsync();
    }





    public async Task RejectAsync(
        long quotationId,
        string userId,
        string comment)
    {

        var quotation =
            await _db.Quotations
            .FirstAsync(x =>
                x.Id == quotationId);



        quotation.Status =
            QuotationStatus.RevisionRequired;



        _db.ApprovalHistories.Add(
            new ApprovalHistory
            {
                QuotationId =
                    quotationId,

                UserId =
                    userId,

                Approved =
                    false,

                Comment =
                    comment
            });



        await _db.SaveChangesAsync();
    }





    public async Task MarkReadyToSendAsync(
        long quotationId)
    {

        var quotation =
            await _db.Quotations
            .FirstAsync(x =>
                x.Id == quotationId);



        if (quotation.Status !=
            QuotationStatus.Approved)
        {
            throw new Exception(
                "Quotation is not approved");
        }



        quotation.Status =
            QuotationStatus.ReadyToSend;



        await _db.SaveChangesAsync();
    }





    public async Task SendToCustomerAsync(
        long quotationId)
    {

        var quotation =
            await _db.Quotations
            .FirstAsync(x =>
                x.Id == quotationId);



        quotation.Status =
            QuotationStatus.SentToCustomer;



        await _db.SaveChangesAsync();
    }




    public async Task CustomerDecisionAsync(
        long quotationId,
        bool accepted)
    {

        var quotation =
            await _db.Quotations
            .FirstAsync(x =>
                x.Id == quotationId);



        quotation.Status =
            accepted
            ? QuotationStatus.Accepted
            : QuotationStatus.Rejected;



        await _db.SaveChangesAsync();
    }
}