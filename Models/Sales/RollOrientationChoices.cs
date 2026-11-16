namespace HamiChap_MES_ERP.Models.Sales;

/// <summary>
/// The four current roll winding / label-reading diagrams. The string values are
/// deliberately distinct from legacy numeric orientations (1-8): historical
/// specifications must not be silently reinterpreted.
/// </summary>
public sealed record RollOrientationChoice(string Value, string LocalizationKey, string ImagePath);

public static class RollOrientationChoices
{
    public static IReadOnlyList<RollOrientationChoice> All { get; } =
    [
        new("horizontal-left", "RollOrientationHorizontalLeft", "/images/roll-orientations/horizontal-left.svg"),
        new("horizontal-right", "RollOrientationHorizontalRight", "/images/roll-orientations/horizontal-right.svg"),
        new("vertical-left", "RollOrientationVerticalLeft", "/images/roll-orientations/vertical-left.svg"),
        new("vertical-right", "RollOrientationVerticalRight", "/images/roll-orientations/vertical-right.svg")
    ];

    public static RollOrientationChoice? Find(string? value) =>
        All.FirstOrDefault(choice => string.Equals(choice.Value, value, StringComparison.Ordinal));

    public static bool IsLegacy(string? value) =>
        int.TryParse(value, out var n) && n is >= 1 and <= 8;
}
