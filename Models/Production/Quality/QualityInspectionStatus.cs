namespace HamiChap_MES_ERP.Models.Production;

public enum QualityInspectionStatus
{
    Pending = 1,

    InProgress = 2,

    Passed = 3,

    Failed = 4,

    PartiallyPassed = 5,

    Cancelled = 6
}