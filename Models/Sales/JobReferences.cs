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
}
