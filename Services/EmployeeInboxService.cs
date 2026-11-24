using System.Security.Claims;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Administration;
using HamiChap_MES_ERP.Models.Inventory;
using Microsoft.EntityFrameworkCore;
using static HamiChap_MES_ERP.Services.EmployeeInboxEvents;

namespace HamiChap_MES_ERP.Services;

public sealed class EmployeeInboxService(IDbContextFactory<ApplicationDbContext> factory,
    PermissionService permissions, WarehouseRequestService warehouse)
{
    public sealed record Employee(string Id, string Name);
    public sealed record Entry(long Id, string Kind, string Subject, string Body, string? SubjectFa, string? BodyFa,
        string Sender, DateTime CreatedAtUtc, bool Unread, string? Url, WarehouseRequestService.RequestRow? Material,
        LowStockAlert? StockAlert);
    public sealed record Snapshot(IReadOnlyList<Entry> Entries, int UnreadCount, int TotalCount, bool CanIssue);
    private sealed record Access(string UserId, bool Warehouse, bool Accounting, bool Shipping, bool Operator, bool Stock);

    private async Task<Access> GetAccessAsync(ClaimsPrincipal user, ApplicationDbContext db)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id) ||
            !await db.Users.AnyAsync(x => x.Id == id && x.IsActive))
            throw new UnauthorizedAccessException("An active employee account is required.");
        return new(id, await warehouse.CanViewAsync(user),
            await permissions.HasPermissionAsync(user, "Sales.Invoices.ConfirmPayment"),
            await permissions.HasPermissionAsync(user, "Sales.Deliveries.View") &&
                await permissions.HasPermissionAsync(user, "Sales.Deliveries.Post"),
            OperatorRoles.Any(user.IsInRole),
            await permissions.HasPermissionAsync(user, "Inventory.Stock.View"));
    }

    private static IQueryable<EmployeeInboxItem> Visible(ApplicationDbContext db, Access access) =>
        db.EmployeeInboxItems.Where(x => x.RecipientUserId == access.UserId &&
            (x.Kind == Message || x.Kind == MaterialIssued || (access.Operator && x.Kind == OperationReady) ||
             (access.Warehouse && x.Kind == MaterialRequest) ||
             (access.Stock && (x.Kind == LowStock || x.Kind == LowStockResolved)) ||
             (access.Accounting && x.Kind == InvoiceReady) ||
             (access.Shipping && x.Kind == ShipmentReady)));

    // Reconcile only for an authorized account. An active shortage is one
    // notification episode, independent of its read/unread status. When a
    // shortage clears, keep its original inbox entry as resolved history.
    private static async Task<Dictionary<int, LowStockAlert>> SynchronizeLowStockAsync(ApplicationDbContext db, string userId)
    {
        var current = (await InventoryAlertService.ReadMonitoredAsync(db))
            .Where(x => x.AvailableQuantity <= x.Threshold).ToDictionary(x => x.ItemId);
        var active = db.EmployeeInboxItems.Where(x => x.RecipientUserId == userId && x.Kind == LowStock);
        var activeIds = await active.AsNoTracking().Select(x => x.SourceId).ToListAsync();
        if (activeIds.Count == current.Count && activeIds.All(x => x.HasValue && x.Value >= int.MinValue &&
            x.Value <= int.MaxValue && current.ContainsKey((int)x.Value)))
            return current;

        // Most polls are read-only. Serialize only when the shortage set changes,
        // then recheck stock within the transaction to prevent duplicate episodes.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        current = (await InventoryAlertService.ReadMonitoredAsync(db))
            .Where(x => x.AvailableQuantity <= x.Threshold).ToDictionary(x => x.ItemId);
        var open = await active.OrderBy(x => x.Id).ToListAsync();
        var retained = new HashSet<long>();
        foreach (var entry in open)
        {
            if (!entry.SourceId.HasValue || entry.SourceId.Value < int.MinValue ||
                entry.SourceId.Value > int.MaxValue || !current.ContainsKey((int)entry.SourceId.Value) ||
                !retained.Add(entry.SourceId.Value))
                entry.Kind = LowStockResolved;
        }

        var missing = current.Values.Where(x => !retained.Contains(x.ItemId)).ToList();
        if (missing.Count > 0)
        {
            var ids = missing.Select(x => (long?)x.ItemId).ToArray();
            var previous = await db.EmployeeInboxItems.AsNoTracking().Where(x => x.RecipientUserId == userId &&
                    (x.Kind == LowStock || x.Kind == LowStockResolved) && ids.Contains(x.SourceId))
                .GroupBy(x => x.SourceId).Select(g => new { ItemId = g.Key, LastId = g.Max(x => x.Id) }).ToListAsync();
            var previousIds = previous.Where(x => x.ItemId.HasValue).ToDictionary(x => x.ItemId!.Value, x => x.LastId);
            foreach (var row in missing)
            {
                var subject = "Low stock: " + row.ItemName;
                var subjectFa = "کسری موجودی: " + row.ItemName;
                db.EmployeeInboxItems.Add(new EmployeeInboxItem
                {
                    RecipientUserId = userId, Kind = LowStock, SourceId = row.ItemId,
                    EventKey = $"low-stock:{row.ItemId}:{previousIds.GetValueOrDefault(row.ItemId)}",
                    Subject = subject.Length <= 400 ? subject : subject[..400],
                    SubjectFa = subjectFa.Length <= 400 ? subjectFa : subjectFa[..400],
                    Body = $"At detection: {row.ItemCode} · Available {row.AvailableQuantity:0.###} {row.UnitSymbol}; minimum {row.Threshold:0.###} {row.UnitSymbol}.",
                    BodyFa = $"هنگام ثبت هشدار: {row.ItemCode} · موجودی {row.AvailableQuantity:0.###} {row.UnitSymbol}؛ حداقل {row.Threshold:0.###} {row.UnitSymbol}.",
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return current;
    }

    public async Task<Snapshot> ReadAsync(ClaimsPrincipal user, bool unreadOnly, int take = 30)
    {
        await using var db = await factory.CreateDbContextAsync();
        var access = await GetAccessAsync(user, db);
        var canIssue = access.Warehouse && await warehouse.CanIssueAsync(user);
        var lowStock = access.Stock ? await SynchronizeLowStockAsync(db, access.UserId) : new Dictionary<int, LowStockAlert>();
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
                MaterialIssued or OperationReady => "/production/machine-operator",
                InvoiceReady when x.SourceId.HasValue => $"/sales/invoices/edit/{x.SourceId.Value}",
                ShipmentReady => "/inventory/shipments",
                LowStock or LowStockResolved => "/inventory/low-stock",
                _ => null
            },
            x.Kind == MaterialRequest && x.SourceId.HasValue ? byId.GetValueOrDefault(x.SourceId.Value) : null,
            x.Kind == LowStock && x.SourceId is >= int.MinValue and <= int.MaxValue ? lowStock.GetValueOrDefault((int)x.SourceId.Value) : null)).ToList(),
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
