using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>
    /// Reuse an earlier item's specifications in the current NEW draft, never
    /// update the historical request or inherit its ordered quantity.
    /// The employee must supply a new positive quantity explicitly.
    /// </summary>
    public static async Task CopySpecificationsIntoDraftAsync(
        ApplicationDbContext db,
        long sourceRequestId,
        long targetRequestId,
        int newQuantity)
    {
        if (newQuantity <= 0)
            throw new InvalidOperationException("Enter a new quantity greater than zero.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        var target = await db.SalesRequests
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == targetRequestId)
            ?? throw new InvalidOperationException("Current job not found.");
        var source = await db.SalesRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == sourceRequestId)
            ?? throw new InvalidOperationException("Saved job not found.");

        if (source.Id == target.Id || target.Status != SalesRequestStatus.Draft ||
            source.CustomerId != target.CustomerId || source.JobType != target.JobType ||
            source.Status == SalesRequestStatus.Cancelled)
            throw new InvalidOperationException(
                "Only a saved job for the same customer and production type can be reused in a new draft.");

        var alreadyLinked =
            await db.Quotations.AnyAsync(q => q.SalesRequestId == target.Id) ||
            await db.QuotationLines.AnyAsync(line => line.SourceRequestId == target.Id);
        if (alreadyLinked)
            throw new InvalidOperationException("Already quoted jobs cannot be overwritten.");

        var json = JsonNode.Parse(source.JobSpecificationJson ?? "{}") as JsonObject
            ?? throw new InvalidOperationException("Saved job specifications are invalid.");
        if (string.IsNullOrWhiteSpace(json["JobTitle"]?.GetValue<string>()))
            throw new InvalidOperationException("Saved job has no title.");

        if (target.JobType == SalesJobType.Roll)
        {
            json["LabelQuantity"] = newQuantity;
        }
        else
        {
            // Clear the old print-run and order quantities together. The new
            // sheet count drives the quotation quantity in SheetJobEdit.
            json["SheetCount"] = newQuantity;
            json["PrintRunSheets"] = newQuantity;
            json["OrderNumber"] = string.Empty;
        }

        // Never keep the old draft's finished-product line when switching
        // designs. The editor regenerates exactly one line on the next save.
        db.SalesRequestLines.RemoveRange(target.Lines);
        target.JobSpecificationJson = json.ToJsonString();
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

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
