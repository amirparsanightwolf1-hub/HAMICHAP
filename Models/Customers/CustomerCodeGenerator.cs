using HamiChap_MES_ERP.Data;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Models.Customers;

/// <summary>Generates customer references without relying on a shared sequential counter.
/// The existing unique database index is the final safeguard against duplicates.</summary>
public static class CustomerCodeGenerator
{
    public static async Task<string> GenerateAsync(ApplicationDbContext db)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = "CUS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            if (!db.Customers.Local.Any(x => x.Code == code) &&
                !await db.Customers.AnyAsync(x => x.Code == code))
                return code;
        }
        throw new InvalidOperationException("Unable to generate a unique customer code.");
    }
}
