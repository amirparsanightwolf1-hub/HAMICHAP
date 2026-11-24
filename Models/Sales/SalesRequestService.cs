using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;
using HamiChap_MES_ERP.Models.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace HamiChap_MES_ERP.Services;

public class SalesRequestService
{
    private readonly ApplicationDbContext _db;
    private readonly NumberSeriesService _numberSeries;
    private readonly QuotationService _quotationService;
    private readonly AuthenticationStateProvider _auth;
    private readonly PermissionService _permissions;

    public SalesRequestService(
        ApplicationDbContext db,
        NumberSeriesService numberSeries,
        QuotationService quotationService,
        AuthenticationStateProvider auth,
        PermissionService permissions)
    {
        _db = db;
        _numberSeries = numberSeries;
        _quotationService = quotationService;
        _auth = auth;
        _permissions = permissions;
    }



    private async Task<ClaimsPrincipal> RequireAsync(string permission)
    {
        var principal = (await _auth.GetAuthenticationStateAsync()).User;
        if (!await _permissions.HasPermissionAsync(principal, permission))
            throw new UnauthorizedAccessException("Required permission missing.");
        return principal;
    }

    public async Task<SalesRequest> CreateRequestAsync(
    int customerId,
    string userId,
    SalesJobType jobType = SalesJobType.Roll)
    {
        var principal = await RequireAsync("Sales.Requests.Create");
        if (principal.FindFirstValue(ClaimTypes.NameIdentifier) != userId)
            throw new UnauthorizedAccessException("Employee identity mismatch.");

        var number =
            await _numberSeries
                .GetNextNumberAsync(
                    NumberSeriesType.Job);


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
        await RequireAsync("Sales.Requests.Edit");

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
        await RequireAsync("Sales.Requests.Edit");

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






    public async Task CreateQuotationAsync(long requestId)
    {
        await RequireAsync("Sales.Requests.CreateQuotation");
        await _quotationService.CreateFromRequestAsync(requestId);
    }

}