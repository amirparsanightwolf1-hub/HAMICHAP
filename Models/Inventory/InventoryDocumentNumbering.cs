using HamiChap_MES_ERP.Models.Shared;
namespace HamiChap_MES_ERP.Models.Inventory;

public static class InventoryDocumentNumbering
{
    public static NumberSeriesType SeriesFor(InventoryDocumentType type) => type switch
    {
        InventoryDocumentType.GoodsReceipt => NumberSeriesType.GoodsReceipt,
        InventoryDocumentType.CustomerReceipt => NumberSeriesType.CustomerReceipt,
        InventoryDocumentType.MaterialTransfer => NumberSeriesType.MaterialTransfer,
        InventoryDocumentType.ProductionIssue => NumberSeriesType.ProductionIssue,
        InventoryDocumentType.ProductionReceipt => NumberSeriesType.ProductionReceipt,
        InventoryDocumentType.InventoryAdjustment => NumberSeriesType.InventoryAdjustment,
        InventoryDocumentType.CustomerReturn => NumberSeriesType.CustomerReturn,
        InventoryDocumentType.SupplierReturn => NumberSeriesType.SupplierReturn,
        InventoryDocumentType.ProductionScrap => NumberSeriesType.ProductionScrap,
        InventoryDocumentType.SalesDelivery => NumberSeriesType.SalesDelivery,
        InventoryDocumentType.ProductionConsumption => NumberSeriesType.ProductionConsumption,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
