using System.Text;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Models.Sales;

/// <summary>Creates or reuses the finished-product item associated with a Roll or Sheet job.
/// The item code keeps the original creation date; later matching jobs reuse that item.
/// </summary>
public static class JobProductItemHelper
{
    public static async Task<Item> GetOrCreateAsync(
        ApplicationDbContext db,
        SalesRequest request,
        string jobTitle,
        string categoryName)
    {
        if (request.Customer == null)
            throw new InvalidOperationException("The sales request customer must be loaded before creating the job item.");

        var title = jobTitle.Trim();
        if (title.Length == 0)
            throw new InvalidOperationException("Job title is required before saving the job specifications.");

        var category = (await db.ItemCategories
                .Where(x => x.IsActive)
                .ToListAsync())
            .FirstOrDefault(x => string.Equals(x.Name.Trim(), categoryName, StringComparison.OrdinalIgnoreCase));

        if (category == null)
            throw new InvalidOperationException(
                $"Active item category '{categoryName}' was not found. Create or activate this category first.");

        var unit = (await db.Units
                .Where(x => x.IsActive)
                .ToListAsync())
            .FirstOrDefault(x => string.Equals(x.Name.Trim(), "عدد", StringComparison.Ordinal));

        if (unit == null)
            throw new InvalidOperationException(
                "The active unit 'عدد' was not found. Create or activate that unit before saving job specifications.");

        var customerPart = NormalizeCodePart(request.Customer.Name).ToUpperInvariant();
        var titlePart = NormalizeCodePart(title).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(customerPart) || string.IsNullOrWhiteSpace(titlePart))
            throw new InvalidOperationException("Customer name and job title must contain letters or numbers to generate an item code.");

        var codePrefix = $"{customerPart}-{titlePart}-";

        // The category distinguishes Roll from Sheet. Name + customer-code prefix lets
        // later requests reuse the same product even when the request date has changed.
        var existing = await db.Items
            .Where(x => x.CategoryId == category.Id && x.Name == title && x.Code.StartsWith(codePrefix))
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            if (!existing.IsActive)
                throw new InvalidOperationException(
                    $"The existing item '{existing.Code}' is inactive. Activate it in the item master before reusing it.");

            if (!existing.IsFinishedProduct || existing.IsRawMaterial)
                throw new InvalidOperationException(
                    $"The existing item '{existing.Code}' is not configured as a finished product. Correct it in the item master before reusing it.");

            if (existing.UnitId != unit.Id)
                throw new InvalidOperationException(
                    $"The existing item '{existing.Code}' does not use unit 'عدد'. Correct it in the item master before reusing it; its unit was not changed automatically.");

            return existing;
        }

        var datePart = request.RequestDate.ToString("yyyyMMdd");
        var code = $"{codePrefix}{datePart}";

        var codeConflict = await db.Items.FirstOrDefaultAsync(x => x.Code == code);
        if (codeConflict != null)
            throw new InvalidOperationException(
                $"Item code '{code}' already exists for a different item. Resolve the code conflict in the item master first.");

        var item = new Item
        {
            Code = code,
            Name = title,
            CategoryId = category.Id,
            UnitId = unit.Id,
            IsActive = true,
            IsRawMaterial = false,
            IsFinishedProduct = true,
            RequiresLotTracking = false
        };

        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private static string NormalizeCodePart(string value)
    {
        var builder = new StringBuilder();
        var pendingSeparator = false;

        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (pendingSeparator && builder.Length > 0)
                    builder.Append('-');
                builder.Append(ch);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }
}
