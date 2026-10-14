namespace HamiChap_MES_ERP.Models.Production;

public enum ProductionOrderStatus
{
    Planned = 1,

    MaterialChecking = 2,

    WaitingForMaterial = 3,

    ReadyToStart = 4,

    InProduction = 5,

    QualityControl = 6,

    Completed = 7,

    Cancelled = 8
}