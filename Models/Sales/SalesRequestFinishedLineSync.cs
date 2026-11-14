using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Sales.Enums;

namespace HamiChap_MES_ERP.Models.Sales;

/// <summary>
/// SalesRequest.Lines is required by the quotation workflow, but it is no longer
/// entered separately. A draft Roll/Sheet job produces exactly one request line:
/// Item = the finished product created for JobTitle, Quantity = the quantity
/// entered in that job's editor, Unit = the finished-product unit (عدد).
/// Already-submitted requests are never rewritten.
/// </summary>
public static class SalesRequestFinishedLineSync
{
    public static void Apply(
        ApplicationDbContext db,
        SalesRequest request,
        Item finishedProduct,
        decimal? quantity,
        int? previousFinishedProductItemId)
    {
        if (request.Status != SalesRequestStatus.Draft)
            return;

        if (!quantity.HasValue || quantity.Value <= 0)
            throw new InvalidOperationException("Enter a quantity greater than zero in the job specifications.");

        var line = request.Lines.FirstOrDefault(x => x.ItemId == finishedProduct.Id);
        if (line is null && previousFinishedProductItemId.HasValue)
            line = request.Lines.FirstOrDefault(x => x.ItemId == previousFinishedProductItemId.Value);

        // Prefer reusing an existing line instead of deleting/reinserting it;
        // this also upgrades older drafts that used a manually entered item.
        line ??= request.Lines.FirstOrDefault();
        if (line is null)
        {
            line = new SalesRequestLine { SalesRequestId = request.Id };
            request.Lines.Add(line);
        }

        line.ItemId = finishedProduct.Id;
        line.Item = finishedProduct;
        line.UnitId = finishedProduct.UnitId;
        line.Quantity = quantity.Value;

        // The editor now has a single finished-product quantity. Drop obsolete
        // draft-only extra lines so quotations cannot accidentally include them.
        foreach (var extra in request.Lines.Where(x => !ReferenceEquals(x, line)).ToArray())
        {
            if (extra.Id > 0)
                db.SalesRequestLines.Remove(extra);
            request.Lines.Remove(extra);
        }
    }
}
