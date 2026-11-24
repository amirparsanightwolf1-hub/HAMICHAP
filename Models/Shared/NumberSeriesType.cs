namespace HamiChap_MES_ERP.Models.Shared;

public enum NumberSeriesType
{
    GoodsReceipt = 1,
    CustomerReceipt = 2,
    MaterialTransfer = 3,
    ProductionIssue = 4,
    ProductionReceipt = 5,
    InventoryAdjustment = 6,
    CustomerReturn = 7,
    SupplierReturn = 8,

    ProductionOrder = 100,

    SalesOrder = 200,
    SalesRequest = 201,
    Quotation = 202,
    SalesDelivery = 203,
    SalesInvoice = 204,
    Job = 205,
    PurchaseOrder = 300,

    ProductionScrap = 500,
    ProductionConsumption = 501
}