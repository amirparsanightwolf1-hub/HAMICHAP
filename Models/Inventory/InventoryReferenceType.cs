namespace HamiChap_MES_ERP.Models.Inventory;

public enum InventoryReferenceType
{
    None = 0,
    PurchaseOrder = 1,
    CustomerReceipt = 2,
    ProductionOrder = 3,
    InventoryTransfer = 4,
    SalesShipment = 5,
    InventoryAdjustment = 6,
    Return = 7
}