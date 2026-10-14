namespace HamiChap_MES_ERP.Models.Sales.Enums;

public enum WorkflowAction
{
    Created = 1,

    Submitted = 2,

    Assigned = 3,

    Evaluated = 4,

    Approved = 5,

    Rejected = 6,

    ReturnedForRevision = 7,

    SentToCustomer = 8,

    AcceptedByCustomer = 9,

    ConvertedToOrder = 10,

    Cancelled = 11
}