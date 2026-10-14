namespace HamiChap_MES_ERP.Models.Production;

public enum MRPStatus
{
    Calculated = 1,

    Approved = 2,

    PurchaseRequired = 3,

    ReadyForProduction = 4,

    Cancelled = 5
}