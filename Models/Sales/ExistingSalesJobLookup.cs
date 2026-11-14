using System.Text.Json;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Sales.Enums;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Models.Sales;

/// <summary>
/// Looks up prior Roll/Sheet jobs by exact, case-insensitive JobTitle.
/// Both customer and job type must match: similarly named jobs belonging
/// to different customers or production types are never mixed.
/// </summary>
public static class ExistingSalesJobLookup
{
    public sealed record Match(long Id, string RequestNumber, SalesRequestStatus Status);

    public static async Task<List<Match>> FindAsync(
        ApplicationDbContext db,
        int customerId,
        SalesJobType jobType,
        string? title,
        long currentRequestId)
    {
        var expectedTitle = title?.Trim();
        if (customerId <= 0 || string.IsNullOrWhiteSpace(expectedTitle))
            return new();

        var candidates = await db.SalesRequests.AsNoTracking()
            .Where(r => r.CustomerId == customerId &&
                        r.JobType == jobType &&
                        r.Id != currentRequestId &&
                        r.Status != SalesRequestStatus.Cancelled)
            .Select(r => new { r.Id, r.RequestNumber, r.Status, r.JobSpecificationJson })
            .ToListAsync();

        var matches = new List<Match>();
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.JobSpecificationJson))
                continue;
            try
            {
                using var doc = JsonDocument.Parse(candidate.JobSpecificationJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    continue;

                // Existing JSON was saved by both Sheet and Roll editors.
                var hasTitle = doc.RootElement.TryGetProperty("JobTitle", out var titleValue);
                if (!hasTitle || titleValue.ValueKind != JsonValueKind.String)
                    continue;

                if (string.Equals(titleValue.GetString()?.Trim(),
                    expectedTitle, StringComparison.OrdinalIgnoreCase))
                    matches.Add(new Match(candidate.Id, candidate.RequestNumber, candidate.Status));
            }
            catch (JsonException)
            {
                // Ignore historical records with malformed specifications.
            }
        }

        return matches.OrderByDescending(x => x.Id).ToList();
    }
}
