using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public sealed class SalesProofService(IDbContextFactory<ApplicationDbContext> factory, PermissionService permissions)
{
    public sealed record State(string Fingerprint, IReadOnlyList<SalesProofRevision> Revisions)
    {
        public SalesProofRevision? Latest => Revisions.FirstOrDefault();
        public bool Accepted => Latest is { Status: ProofRevisionStatus.Accepted } && Latest.Fingerprint == Fingerprint;
        public bool AwaitingResponse => Latest is { Status: ProofRevisionStatus.Sent } && Latest.Fingerprint == Fingerprint;
    }

    private static string Payload(SalesRequest request) => JsonSerializer.Serialize(new
    {
        request.RequestNumber, request.CustomerId, CustomerName = request.Customer?.Name,
        request.JobType, request.RequestDate, request.JobSpecificationJson,
        Lines = request.Lines.OrderBy(x => x.Id).Select(x => new { x.ItemId, x.UnitId, x.Quantity, x.Notes }).ToArray()
    });

    public static string Fingerprint(SalesRequest request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Payload(request))));

    private static async Task<SalesRequest> LoadRequestAsync(ApplicationDbContext db, long id) =>
        await db.SalesRequests.AsNoTracking().Include(x => x.Customer).Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id) ?? throw new InvalidOperationException("Job not found.");

    public async Task<State> ReadAsync(long id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var request = await LoadRequestAsync(db, id);
        var revisions = await db.SalesProofRevisions.AsNoTracking().Where(x => x.SalesRequestId == id)
            .OrderByDescending(x => x.RevisionNumber).ToListAsync();
        return new(Fingerprint(request), revisions);
    }

    private async Task<string> RequireEmployeeAsync(ClaimsPrincipal user)
    {
        if (!await permissions.HasPermissionAsync(user, "Sales.Requests.Edit"))
            throw new UnauthorizedAccessException("Sales request editing permission is required.");
        return user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("Employee identity is required.");
    }

    private static async Task EnsureOpenAsync(ApplicationDbContext db, SalesRequest request)
    {
        if (request.Status == SalesRequestStatus.Cancelled ||
            await db.Quotations.AnyAsync(q => (q.SalesRequestId == request.Id || q.Lines.Any(x => x.SourceRequestId == request.Id)) &&
                (q.Status == QuotationStatus.Accepted || q.Status == QuotationStatus.Cancelled || q.Status == QuotationStatus.Rejected || q.Status == QuotationStatus.Expired)))
            throw new InvalidOperationException("This job is closed.");
    }

    public async Task MarkSentAsync(long id, string displayedFingerprint, ClaimsPrincipal user)
    {
        var employee = await RequireEmployeeAsync(user);
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var request = await LoadRequestAsync(db, id);
        await EnsureOpenAsync(db, request);
        if (request.Lines.Count == 0 || request.Lines.Any(x => x.Quantity <= 0))
            throw new InvalidOperationException("Save the job and its positive quantity first.");
        var hash = Fingerprint(request);
        if (hash != displayedFingerprint)
            throw new InvalidOperationException("The job has changed. Open the latest proof first.");
        var latest = await db.SalesProofRevisions.Where(x => x.SalesRequestId == id)
            .OrderByDescending(x => x.RevisionNumber).FirstOrDefaultAsync();
        if (latest != null && latest.Fingerprint == hash && latest.Status is ProofRevisionStatus.Sent or ProofRevisionStatus.Accepted)
            return;
        if (latest?.Status == ProofRevisionStatus.Sent) latest.Status = ProofRevisionStatus.Superseded;
        db.SalesProofRevisions.Add(new SalesProofRevision
        {
            SalesRequestId = id, RevisionNumber = (latest?.RevisionNumber ?? 0) + 1,
            SnapshotJson = Payload(request), Fingerprint = hash, Status = ProofRevisionStatus.Sent,
            SentAtUtc = DateTime.UtcNow, SentByUserId = employee
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task RecordDecisionAsync(long id, string displayedFingerprint, bool accepted, string notes, ClaimsPrincipal user)
    {
        var employee = await RequireEmployeeAsync(user);
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var request = await LoadRequestAsync(db, id);
        await EnsureOpenAsync(db, request);
        var latest = await db.SalesProofRevisions.Where(x => x.SalesRequestId == id)
            .OrderByDescending(x => x.RevisionNumber).FirstOrDefaultAsync();
        if (latest?.Status != ProofRevisionStatus.Sent || latest.Fingerprint != displayedFingerprint ||
            latest.Fingerprint != Fingerprint(request))
            throw new InvalidOperationException("Only the current sent proof can receive a customer decision.");
        latest.Status = accepted ? ProofRevisionStatus.Accepted : ProofRevisionStatus.ChangesRequested;
        latest.DecisionAtUtc = DateTime.UtcNow;
        latest.DecisionByUserId = employee;
        latest.DecisionNotes = notes.Trim();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task CancelUnquotedJobAsync(long id, ClaimsPrincipal user)
    {
        await RequireEmployeeAsync(user);
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var request = await db.SalesRequests.FirstAsync(x => x.Id == id);
        if (await db.Quotations.AnyAsync(q => q.SalesRequestId == id || q.Lines.Any(x => x.SourceRequestId == id)))
            throw new InvalidOperationException("Cancel the linked quotation to cancel this order.");
        if (request.Status == SalesRequestStatus.Cancelled) return;
        request.Status = SalesRequestStatus.Cancelled;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    // Called inside the caller's transaction. A changed job can never inherit approval
    // of a different proof revision. Every item in a shared quotation must pass.
    public static async Task RequireAcceptedProofsAsync(ApplicationDbContext db, long quotationId)
    {
        var quote = await db.Quotations.AsNoTracking().Include(q => q.Lines).FirstAsync(q => q.Id == quotationId);
        var ids = quote.Lines.Where(x => x.SourceRequestId.HasValue).Select(x => x.SourceRequestId!.Value)
            .Append(quote.SalesRequestId).Distinct().ToArray();
        foreach (var id in ids)
        {
            var request = await LoadRequestAsync(db, id);
            var latest = await db.SalesProofRevisions.AsNoTracking().Where(x => x.SalesRequestId == id)
                .OrderByDescending(x => x.RevisionNumber).FirstOrDefaultAsync();
            if (request.Status == SalesRequestStatus.Cancelled || latest?.Status != ProofRevisionStatus.Accepted ||
                latest.Fingerprint != Fingerprint(request))
                throw new InvalidOperationException($"Customer proof approval required for {request.RequestNumber}.");
        }
    }
}
