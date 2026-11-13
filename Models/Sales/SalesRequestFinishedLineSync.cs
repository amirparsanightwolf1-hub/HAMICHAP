using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Sales.Enums;

namespace HamiChap_MES_ERP.Models.Sales;

/// <summary>
/// Keeps the requested finished-product line in sync with the Roll/Sheet
/// specification. Quotes copy SalesRequest.Lines, so the specifications must
/// populate that collection rather than living only in JobSpecificationJson.
/// Additional manually entered request lines are left untouched.
/// </summary>
public static class SalesRequestFinishedLineSync
{
    public static void Apply(
        SalesRequest request,
        Item finishedProduct,
        decimal? quantity,
        int? previousFinishedProductItemId)
    {
        if (request.Status != SalesRequestStatus.Draft ||
            !quantity.HasValue || quantity.Value <= 0)
            return;

        // Existing line for this product takes priority. If the job title was
        // changed, reassign its previous finished-product line instead.
        var line = request.Lines.FirstOrDefault(x => x.ItemId == finishedProduct.Id);
        if (line is null && previousFinishedProductItemId.HasValue)
            line = request.Lines.FirstOrDefault(x => x.ItemId == previousFinishedProductItemId.Value);

        if (line is null)
        {
            request.Lines.Add(new SalesRequestLine
            {
                SalesRequestId = request.Id,
                ItemId = finishedProduct.Id,
                UnitId = finishedProduct.UnitId,
                Quantity = quantity.Value
            });
            return;
        }

        line.ItemId = finishedProduct.Id;
        line.Item = finishedProduct;
        line.UnitId = finishedProduct.UnitId;
        line.Quantity = quantity.Value;
    }
}
