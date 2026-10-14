namespace HamiChap_MES_ERP.Models.Shared;

public enum NumberSeriesType
{
    GoodsReceipt = 1,
    CustomerReceipt = 2,
    MaterialTransfer = 3,
    ProductionIssue = 4,
    ProductionReceipt = 5,
    InventoryAdjustment = 6,

    ProductionOrder = 100,

    SalesOrder = 200,
    SalesRequest = 201,
    Quotation = 202,
    SalesDelivery = 203,

    PurchaseOrder = 300,

    ProductionScrap = 500
}