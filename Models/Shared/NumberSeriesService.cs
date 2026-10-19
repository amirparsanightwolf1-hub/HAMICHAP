using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Models.Shared;

public class NumberSeriesService
{
    private readonly ApplicationDbContext _db;

    public NumberSeriesService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> GetNextNumberAsync(
    NumberSeriesType type,
    bool useTransaction = true)
    {
        if (!useTransaction ||
            _db.Database.CurrentTransaction != null)
        {
            return await GetNextNumberInternalAsync(type);
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var number =
                await GetNextNumberInternalAsync(type);

            await transaction.CommitAsync();

            return number;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<string> GetNextNumberInternalAsync(
    NumberSeriesType type)
    {
        var year = DateTime.Now.Year;

        var series =
            await _db.NumberSeries
                .FirstOrDefaultAsync(x =>
                    x.Type == type &&
                    x.Year == year);

        if (series == null)
        {
            series = new NumberSeries
            {
                Type = type,
                Prefix = GetPrefix(type),
                Year = year,
                LastNumber = 0,
                Padding = 6
            };

            _db.NumberSeries.Add(series);

            await _db.SaveChangesAsync();
        }

        series.LastNumber++;

        await _db.SaveChangesAsync();

        return
            $"{series.Prefix}-{year}-" +
            $"{series.LastNumber.ToString().PadLeft(series.Padding, '0')}";
    }

    private static string GetPrefix(NumberSeriesType type)
    {
        return type switch
        {
            NumberSeriesType.GoodsReceipt => "GR",
            NumberSeriesType.CustomerReceipt => "CR",
            NumberSeriesType.MaterialTransfer => "MT",
            NumberSeriesType.ProductionIssue => "PI",
            NumberSeriesType.ProductionReceipt => "PR",
            NumberSeriesType.InventoryAdjustment => "IA",
            NumberSeriesType.ProductionOrder => "PO",
            NumberSeriesType.SalesOrder => "SO",
            NumberSeriesType.PurchaseOrder => "PU",
            NumberSeriesType.SalesRequest => "SR",
            NumberSeriesType.Quotation => "QT",
            NumberSeriesType.ProductionScrap => "PS",
            NumberSeriesType.SalesDelivery => "SD",

            _ => "DOC"
        };
    }
}