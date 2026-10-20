using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;
using HamiChap_MES_ERP.Models.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class SalesRequestService
{
    private readonly ApplicationDbContext _db;
    private readonly NumberSeriesService _numberSeries;
    private readonly QuotationService _quotationService;

    public SalesRequestService(
        ApplicationDbContext db,
        NumberSeriesService numberSeries,
        QuotationService quotationService)
    {
        _db = db;
        _numberSeries = numberSeries;
        _quotationService = quotationService;
    }



    public async Task<SalesRequest> CreateRequestAsync(
    int customerId,
    string userId,
    SalesJobType jobType = SalesJobType.Roll)
    {

        var number =
            await _numberSeries
                .GetNextNumberAsync(
                    NumberSeriesType.SalesRequest);


        var request = new SalesRequest
        {
            JobType = jobType,

            RequestNumber = number,

            CustomerId = customerId,

            CreatedByUserId = userId,

            RequestDate = DateTime.Now,

            Status = SalesRequestStatus.Draft
        };


        _db.SalesRequests.Add(request);

        await _db.SaveChangesAsync();


        return request;
    }




    public async Task AddLineAsync(
        long requestId,
        int itemId,
        decimal quantity,
        string notes = "")
    {

        var item =
            await _db.Items
                .FirstOrDefaultAsync(x =>
                    x.Id == itemId);


        if (item == null)
            throw new Exception(
                "Item not found");


        var line = new SalesRequestLine
        {
            SalesRequestId = requestId,

            ItemId = itemId,

            Quantity = quantity,

            UnitId = item.UnitId,

            Notes = notes
        };


        _db.SalesRequestLines.Add(line);


        await _db.SaveChangesAsync();
    }




    public async Task SubmitAsync(
        long requestId)
    {

        var request =
            await _db.SalesRequests
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x =>
                    x.Id == requestId);


        if (request == null)
            throw new Exception(
                "Request not found");


        if (!request.Lines.Any())
            throw new Exception(
                "Cannot submit empty request");



        request.Status =
            SalesRequestStatus.Submitted;


        await _db.SaveChangesAsync();
    }





    public async Task AssignEvaluatorAsync(
        long requestId,
        string employeeId)
    {

        var request =
            await _db.SalesRequests
                .FirstAsync(x =>
                    x.Id == requestId);


        request.AssignedToUserId =
            employeeId;


        request.Status =
            SalesRequestStatus.UnderEvaluation;


        await _db.SaveChangesAsync();
    }





    public async Task CompleteEvaluationAsync(
        long requestId)
    {

        var request =
            await _db.SalesRequests
                .FirstAsync(x =>
                    x.Id == requestId);



        request.Status =
            SalesRequestStatus.EvaluationCompleted;



        await _db.SaveChangesAsync();
    }





    public async Task CreateQuotationAsync(long requestId)
    {
        await _quotationService.CreateFromRequestAsync(requestId);
    


        var request =
            await _db.SalesRequests
                .FirstAsync(x =>
                    x.Id == requestId);



        if (request.Status !=
            SalesRequestStatus.EvaluationCompleted)
        {
            throw new Exception(
                "Request must be evaluated first");
        }



        request.Status =
            SalesRequestStatus.QuotationCreated;



        await _db.SaveChangesAsync();
    }

}