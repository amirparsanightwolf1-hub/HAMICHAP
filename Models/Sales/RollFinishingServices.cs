namespace HamiChap_MES_ERP.Models.Sales;

// The stored Value is intentionally unchanged for existing jobs.
// Labels and group names are resolved through LocalizationService.
public sealed record RollFinishingChoice(string Value, string LabelKey);
public sealed record RollFinishingGroup(string TitleKey, IReadOnlyList<RollFinishingChoice> Services);

public static class RollFinishingServices
{
    public static IReadOnlyList<RollFinishingGroup> Groups { get; } =
    [
        new("ServiceGroupUvCoating",
        [
            new("یووی روتاری", "ServiceUvRotary"),
            new("یووی برجسته", "ServiceUvEmbossed"),
            new("یووی تخت", "ServiceUvFlat"),
            new("یووی موضعی", "ServiceUvSpot")
        ]),
        new("ServiceGroupLamination",
        [
            new("سلفون مات", "ServiceMatteLamination"),
            new("سلفون براق", "ServiceGlossLamination")
        ]),
        new("ServiceGroupFoilStamping",
        [
            new("طلاکوب روتاری", "ServiceRotaryFoil"),
            new("طلاکوب تخت", "ServiceFlatFoil"),
            new("رنگ فویل", "ServiceFoilColor")
        ]),
        new("ServiceGroupEmbossingDie",
        [
            new("قالب روتاری", "ServiceRotaryDie"),
            new("قالب تخت", "ServiceFlatDie"),
            new("دیلن میلن", "ServiceDylanMilan")
        ])
    ];
}
