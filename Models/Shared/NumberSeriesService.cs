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
        // Preserve the existing caller API. Join a caller's transaction when
        // present; otherwise always create one so the allocated number and
        // readback are protected even for legacy useTransaction:false callers.
        if (_db.Database.CurrentTransaction != null)
            return await AllocateAsync(type);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var number = await AllocateAsync(type);
        await transaction.CommitAsync();
        return number;
    }

    private async Task<string> AllocateAsync(NumberSeriesType type)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));

        var year = DateTime.Now.Year;
        var prefix = GetPrefix(type);

        // Atomic increment under update/range locks. HOLDLOCK also protects
        // the first allocation of a new (Type, Year) key, including when the
        // surrounding business transaction uses READ COMMITTED. The existing
        // unique IX_NumberSeries_Type_Year index supports this key lookup.
        // Values are parameters; no tracked entity or stale counter is used.
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [NumberSeries] WITH (UPDLOCK, HOLDLOCK)
            SET [LastNumber] = [LastNumber] + 1
            WHERE [Type] = {(int)type} AND [Year] = {year};
            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO [NumberSeries]
                    ([Type], [Year], [Prefix], [LastNumber], [Padding], [IsActive])
                VALUES ({(int)type}, {year}, {prefix}, 1, 6, 1);
            END;
            """);

        var series = await _db.NumberSeries.AsNoTracking()
            .SingleAsync(x => x.Type == type && x.Year == year);
        return $"{series.Prefix}-{year}-{series.LastNumber.ToString().PadLeft(series.Padding, '0')}";
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
            NumberSeriesType.CustomerReturn => "CRT",
            NumberSeriesType.SupplierReturn => "SRT",
            NumberSeriesType.ProductionOrder => "PO",
            NumberSeriesType.SalesOrder => "SO",
            NumberSeriesType.PurchaseOrder => "PU",
            NumberSeriesType.SalesRequest => "SR",
            NumberSeriesType.Job => "J",
            NumberSeriesType.Quotation => "QT",
            NumberSeriesType.ProductionScrap => "PS",
            NumberSeriesType.ProductionConsumption => "PC",
            NumberSeriesType.SalesDelivery => "SD",
            NumberSeriesType.SalesInvoice => "SI",

            _ => "DOC"
        };
    }
}