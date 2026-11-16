namespace HamiChap_MES_ERP.Models.Sales;

public sealed record RollOrientationChoice(string Value, string LocalizationKey, string ImagePath);

public static class RollOrientationChoices
{
    public static IReadOnlyList<RollOrientationChoice> All { get; } =
    [
        new("vertical-left", "RollOrientationVerticalLeft", "/images/roll-orientations/vertical-left.png"),
        new("vertical-right", "RollOrientationVerticalRight", "/images/roll-orientations/vertical-right.png"),
        new("horizontal-left", "RollOrientationHorizontalLeft", "/images/roll-orientations/horizontal-left.png"),
        new("horizontal-right", "RollOrientationHorizontalRight", "/images/roll-orientations/horizontal-right.png")
    ];

    public static RollOrientationChoice? Find(string? value) =>
        All.FirstOrDefault(choice => string.Equals(choice.Value, value, StringComparison.Ordinal));

    public static bool IsLegacy(string? value) =>
        int.TryParse(value, out var n) && n is >= 1 and <= 8;
}