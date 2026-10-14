namespace HamiChap_MES_ERP.Models.Sales.Enums;

public enum QuotationStatus
{
    Draft = 1,

    PendingApproval = 2,

    RevisionRequired = 3,

    Approved = 4,

    ReadyToSend = 5,

    SentToCustomer = 6,

    CustomerNegotiation = 7,

    Accepted = 8,

    Rejected = 9,

    Expired = 10,

    Cancelled = 11
}