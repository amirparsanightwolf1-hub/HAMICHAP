using System.Security.Claims;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Administration;
using Microsoft.EntityFrameworkCore;
using static HamiChap_MES_ERP.Services.EmployeeInboxEvents;

namespace HamiChap_MES_ERP.Services;

public sealed class EmployeeInboxService(IDbContextFactory<ApplicationDbContext> factory,
    PermissionService permissions, WarehouseRequestService warehouse)
{
    public sealed record Employee(string Id, string Name);
    public sealed record Entry(long Id, string Kind, string Subject, string Body, string? SubjectFa, string? BodyFa,
        string Sender, DateTime CreatedAtUtc, bool Unread, string? Url, WarehouseRequestService.RequestRow? Material);
    public sealed record Snapshot(IReadOnlyList<Entry> Entries, int UnreadCount, int TotalCount, bool CanIssue);
    private sealed record Access(string UserId, bool Warehouse, bool Accounting, bool Shipping);

    private async Task<Access> GetAccessAsync(ClaimsPrincipal user, ApplicationDbContext db)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id) ||
            !await db.Users.AnyAsync(x => x.Id == id && x.IsActive))
            throw new UnauthorizedAccessException("An active employee account is required.");
        return new(id, await warehouse.CanViewAsync(user),
            await permissions.HasPermissionAsync(user, "Sales.Invoices.ConfirmPayment"),
            await permissions.HasPermissionAsync(user, "Sales.Deliveries.View") &&
                await permissions.HasPermissionAsync(user, "Sales.Deliveries.Post"));
    }

    private static IQueryable<EmployeeInboxItem> Visible(ApplicationDbContext db, Access access) =>
        db.EmployeeInboxItems.Where(x => x.RecipientUserId == access.UserId &&
            (x.Kind == Message || x.Kind == MaterialIssued ||
             (access.Warehouse && x.Kind == MaterialRequest) ||
             (access.Accounting && x.Kind == InvoiceReady) ||
             (access.Shipping && x.Kind == ShipmentReady)));

    public async Task<Snapshot> ReadAsync(ClaimsPrincipal user, bool unreadOnly, int take = 30)
    {
        await using var db = await factory.CreateDbContextAsync();
        var access = await GetAccessAsync(user, db);
        var canIssue = access.Warehouse && await warehouse.CanIssueAsync(user);
        var materials = access.Warehouse
            ? await warehouse.ReadAsync(user, false)
            : Array.Empty<WarehouseRequestService.RequestRow>();
        // Backfill open requests for newly authorized employees and requests created
        // before the inbox migration. The unique per-account key preserves read state.
        if (materials.Count > 0)
        {
            var keys = materials.Select(x => MaterialRequest + ":" + x.Id).ToArray();
            var known = await db.EmployeeInboxItems.AsNoTracking().Where(x => x.RecipientUserId == access.UserId && keys.Contains(x.EventKey))
                .Select(x => x.EventKey).ToListAsync();
            if (known.Count < keys.Length)
            {
                await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var existing = (await db.EmployeeInboxItems.Where(x => x.RecipientUserId == access.UserId && keys.Contains(x.EventKey))
                    .Select(x => x.EventKey).ToListAsync()).ToHashSet();
                foreach (var row in materials.Where(x => !existing.Contains(MaterialRequest + ":" + x.Id)))
                {
                    var detail = string.Join(" · ", row.Lines.Select(x => $"{x.Item}: {x.Required:0.###} {x.Unit}"));
                    Add(db, new[] { access.UserId }, MaterialRequest, row.Id, "Material request: " + row.JobTitle, detail,
                        "درخواست مواد: " + row.JobTitle, detail);
                }
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
        }
        var query = Visible(db, access).AsNoTracking();
        var unread = await query.CountAsync(x => x.ReadAtUtc == null);
        if (unreadOnly) query = query.Where(x => x.ReadAtUtc == null);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 500)).ToListAsync();
        var senders = items.Select(x => x.SenderUserId).OfType<string>().Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(x => senders.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => (x.FirstName + " " + x.LastName).Trim());
        var byId = materials.ToDictionary(x => x.Id);
        return new(items.Select(x => new Entry(x.Id, x.Kind, x.Subject, x.Body, x.SubjectFa, x.BodyFa,
            x.SenderUserId != null ? names.GetValueOrDefault(x.SenderUserId, "") : "",
            x.CreatedAtUtc, x.ReadAtUtc == null,
            x.Kind switch
            {
                MaterialRequest => "/inventory/material-requests",
                MaterialIssued => "/production/machine-operator",
                InvoiceReady when x.SourceId.HasValue => $"/sales/invoices/edit/{x.SourceId.Value}",
                ShipmentReady => "/inventory/shipments",
                _ => null
            },
            x.Kind == MaterialRequest && x.SourceId.HasValue ? byId.GetValueOrDefault(x.SourceId.Value) : null)).ToList(),
            unread, total, canIssue);
    }

    public async Task SetReadAsync(ClaimsPrincipal user, long id, bool read)
    {
        await using var db = await factory.CreateDbContextAsync();
        var access = await GetAccessAsync(user, db);
        var now = read ? (DateTime?)DateTime.UtcNow : null;
        await Visible(db, access).Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAtUtc, now));
    }

    public async Task MarkAllReadAsync(ClaimsPrincipal user)
    {
        await using var db = await factory.CreateDbContextAsync();
        var access = await GetAccessAsync(user, db);
        var now = (DateTime?)DateTime.UtcNow;
        await Visible(db, access).Where(x => x.ReadAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAtUtc, now));
    }

    public async Task<List<Employee>> FindEmployeesAsync(ClaimsPrincipal user, string search)
    {
        await using var db = await factory.CreateDbContextAsync();
        var access = await GetAccessAsync(user, db);
        search = search.Trim();
        if (search.Length > 100) search = search[..100];
        return await db.Users.AsNoTracking().Where(x => x.IsActive && x.Id != access.UserId &&
            (search == "" || (x.FirstName + " " + x.LastName).Contains(search) || x.EmployeeNumber.Contains(search)))
            .OrderBy(x => x.FirstName).ThenBy(x => x.LastName).Take(50)
            .Select(x => new Employee(x.Id, (x.FirstName + " " + x.LastName).Trim() + " · " + x.EmployeeNumber))
            .ToListAsync();
    }

    public async Task SendAsync(ClaimsPrincipal user, string recipient, string subject, string body)
    {
        await using var db = await factory.CreateDbContextAsync();
        var access = await GetAccessAsync(user, db);
        subject = subject.Trim(); body = body.Trim();
        if (subject.Length == 0 || subject.Length > 200 || body.Length == 0 || body.Length > 4000)
            throw new InvalidOperationException("Enter a subject (up to 200 characters) and message (up to 4000 characters).");
        if (recipient == access.UserId || !await db.Users.AnyAsync(x => x.Id == recipient && x.IsActive))
            throw new InvalidOperationException("Select an active employee.");
        db.EmployeeInboxItems.Add(new EmployeeInboxItem
        {
            RecipientUserId = recipient, SenderUserId = access.UserId, EventKey = Guid.NewGuid().ToString("N"),
            Kind = Message, Subject = subject, Body = body, CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
