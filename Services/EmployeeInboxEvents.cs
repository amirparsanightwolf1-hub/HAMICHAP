using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Administration;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

// Call within the originating business transaction, before its final SaveChanges.
public static class EmployeeInboxEvents
{
    public const string MaterialRequest = "material-request";
    public const string MaterialIssued = "material-issued";
    public const string InvoiceReady = "invoice-ready";
    public const string ShipmentReady = "shipment-ready";
    public const string Message = "message";

    public static async Task<IReadOnlyList<string>> RecipientsAsync(ApplicationDbContext db, params string[] required)
    {
        var query = db.Users.Where(u => u.IsActive);
        foreach (var permission in required)
        {
            var name = permission;
            query = query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id &&
                (db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "ADMINISTRATOR") ||
                 db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId && rp.Permission.Name == name))));
        }
        return await query.Select(x => x.Id).Distinct().ToListAsync();
    }

    public static void Add(ApplicationDbContext db, IEnumerable<string> recipients, string kind, long sourceId,
        string subject, string body, string subjectFa, string bodyFa, string? sender = null)
    {
        foreach (var id in recipients.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            db.EmployeeInboxItems.Add(new EmployeeInboxItem
            {
                RecipientUserId = id, SenderUserId = sender, EventKey = kind + ":" + sourceId,
                Kind = kind, SourceId = sourceId, Subject = Clip(subject), Body = body,
                SubjectFa = Clip(subjectFa), BodyFa = bodyFa, CreatedAtUtc = DateTime.UtcNow
            });
    }

    private static string Clip(string value) => value.Length <= 400 ? value : value[..400];
}
