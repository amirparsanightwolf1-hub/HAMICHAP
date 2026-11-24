using System.Text.Json;
using HamiChap_MES_ERP.Models.Production;

namespace HamiChap_MES_ERP.Models.Sales;

/// <summary>Sales requests are the canonical job identities. Documents may span
/// several jobs; the original document numbers are retained for audit history.</summary>
public static class JobReferences
{
    public static IReadOnlyList<SalesRequest> ForQuotation(Quotation? quotation)
    {
        if (quotation is null) return Array.Empty<SalesRequest>();
        return quotation.Lines.Select(x => x.SourceRequest)
            .Append(quotation.SalesRequest)
            .OfType<SalesRequest>()
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .OrderBy(x => x.RequestNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string Display(Quotation? quotation)
    {
        var references = ForQuotation(quotation);
        return references.Count == 0 ? "—" : string.Join(" / ", references.Select(x => x.RequestNumber));
    }

    /// <summary>Read the canonical title from the saved job specification.
    /// Untitled or legacy jobs retain their unique number as a fallback.</summary>
    public static string Title(SalesRequest? request)
    {
        if (request is null) return "—";
        try
        {
            using var json = JsonDocument.Parse(request.JobSpecificationJson ?? "{}");
            if (json.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var property in json.RootElement.EnumerateObject())
                    if (property.Name.Equals("JobTitle", StringComparison.OrdinalIgnoreCase) &&
                        property.Value.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(property.Value.GetString()))
                        return property.Value.GetString()!.Trim();
        }
        catch (JsonException) { }
        return string.IsNullOrWhiteSpace(request.RequestNumber) ? "—" : request.RequestNumber;
    }

    public static string Titles(Quotation? quotation, string fallback = "—")
    {
        var jobs = ForQuotation(quotation);
        return jobs.Count == 0 ? fallback : string.Join(" / ", jobs.Select(Title));
    }

    public static string Label(Quotation? quotation, string fallback = "—")
    {
        var jobs = ForQuotation(quotation);
        return jobs.Count == 0 ? fallback : string.Join(" / ", jobs.Select(x =>
            Title(x) == x.RequestNumber ? x.RequestNumber : $"{Title(x)} · {x.RequestNumber}"));
    }

    public static string ProductionTitle(ProductionOrder? order) =>
        Titles(order?.SalesOrder?.Quotation, order?.OrderNumber ?? "—");

    public static string ProductionLabel(ProductionOrder? order) =>
        Label(order?.SalesOrder?.Quotation, order?.OrderNumber ?? "—");
}
