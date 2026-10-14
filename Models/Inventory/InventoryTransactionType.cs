namespace HamiChap_MES_ERP.Models.Inventory;

public enum InventoryTransactionType 
{
    Receive = 1,

    Transfer = 2,

    Consume = 3,

    Produce = 4,

    Return = 5,

    Adjustment = 6,

    Scrap = 7
}