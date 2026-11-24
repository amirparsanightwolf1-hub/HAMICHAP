using System.Security.Claims;
using System.Text.Json;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

// Internal exchange package for a verified accounting/tax-provider integration.
// This is deliberately NOT an authority-specific signed e-invoice or submission.
public sealed class InvoiceTaxReportingService(IDbContextFactory<ApplicationDbContext> factory, PermissionService permissions)
{
    public const string Permission = "Sales.Invoices.TaxReporting";
    public sealed record Row(long Id, string Number, string Job, DateTime Date, SalesInvoiceType? Type,
        string? Currency, decimal Net, decimal? Vat, decimal Total, string Status, string? Reference);
    public sealed record AuditEvent(DateTime AtUtc, string UserId, string Status, string? Reference, string? Notes);
    private async Task<string> RequireAsync(ApplicationDbContext db, ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id) || !await permissions.HasPermissionAsync(user, Permission) ||
            !await db.Users.AnyAsync(x => x.Id == id && x.IsActive))
            throw new UnauthorizedAccessException("Tax reporting permission required.");
        return id;
    }
    public async Task<List<Row>> ReadAsync(ClaimsPrincipal user, string search, string status)
    {
        await using var db = await factory.CreateDbContextAsync();
        await RequireAsync(db, user);
        var query = db.SalesInvoices.AsNoTracking().Where(x => x.Status == SalesInvoiceStatus.Issued);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.InvoiceNumber.Contains(search) || x.Customer.Name.Contains(search));
        if (status == "legacy") query = query.Where(x => x.TaxReportStatus == null);
        else if (status != "") query = query.Where(x => x.TaxReportStatus == status);
        var invoices = await query.Include(x => x.Quotation).ThenInclude(x => x.SalesRequest)
            .Include(x => x.Quotation).ThenInclude(x => x.Lines).ThenInclude(x => x.SourceRequest)
            .AsSplitQuery().OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.Id).Take(200).ToListAsync();
        return invoices.Select(x => new Row(x.Id, x.InvoiceNumber, JobReferences.Titles(x.Quotation, x.InvoiceNumber), x.InvoiceDate,
            x.InvoiceType, x.CurrencyCode, x.Subtotal, x.VatAmount, x.TotalAmount, x.TaxReportStatus ?? "Legacy — unclassified", x.TaxAuthorityReference)).ToList();
    }
    public async Task<string> PrepareAsync(long id, ClaimsPrincipal user)
    {
        await using var db = await factory.CreateDbContextAsync();
        var employee = await RequireAsync(db, user);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var invoice = await db.SalesInvoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new InvalidOperationException("Invoice not found.");
        if (invoice.Status != SalesInvoiceStatus.Issued) throw new InvalidOperationException("Invoice is not issued.");
        if (invoice.TaxReportJson != null) return invoice.TaxReportJson;
        var seller = TaxSellerProfile.Read(invoice.SellerProfileJson) ?? throw new InvalidOperationException("Missing registered seller snapshot.");
        var buyer = InvoiceBuyerProfile.Read(invoice.BuyerProfileJson);
        if (invoice.InvoiceType == null || invoice.CurrencyCode != seller.CurrencyCode || !invoice.VatAmount.HasValue ||
            invoice.Lines.Count == 0 || invoice.Lines.Any(x => x.VatRate == null || x.VatAmount == null ||
                string.IsNullOrWhiteSpace(x.TaxItemCode) || string.IsNullOrWhiteSpace(x.TaxUnit)))
            throw new InvalidOperationException("Invoice lacks complete tax metadata. Historical invoices require accounting reconciliation.");
        if (invoice.InvoiceType == SalesInvoiceType.Formal && buyer == null)
            throw new InvalidOperationException("Missing formal buyer snapshot.");
        if (seller.Jurisdiction == "IR" && string.IsNullOrWhiteSpace(seller.TaxMemoryId))
            throw new InvalidOperationException("Iran reporting requires the registered tax memory identifier. Resolve seller registration before issuing.");
        if (invoice.Lines.Sum(x => x.LineTotal) != invoice.Subtotal ||
            invoice.Lines.Sum(x => x.VatAmount!.Value) != invoice.VatAmount ||
            invoice.Subtotal + invoice.VatAmount != invoice.TotalAmount)
            throw new InvalidOperationException("Invoice tax totals are inconsistent.");
        invoice.TaxReportJson = JsonSerializer.Serialize(new
        {
            schema = "hamichap.invoice-tax-exchange.v1",
            notice = "Internal exchange package. Not a signed or authority-accepted electronic invoice.",
            invoiceNumber = invoice.InvoiceNumber, invoiceDate = invoice.InvoiceDate,
            documentType = invoice.InvoiceType.ToString(), jurisdiction = seller.Jurisdiction,
            currency = invoice.CurrencyCode, seller, buyer,
            lines = invoice.Lines.OrderBy(x => x.Id).Select(x => new
            {
                itemCode = x.TaxItemCode, name = x.TaxItemName, unit = x.TaxUnit, quantity = x.Quantity,
                netUnitPrice = x.UnitPrice, net = x.LineTotal, vatRate = x.VatRate, vat = x.VatAmount,
                taxTreatmentReason = x.TaxTreatmentReason, gross = x.LineTotal + x.VatAmount, description = x.Notes
            }),
            subtotal = invoice.Subtotal, vat = invoice.VatAmount, total = invoice.TotalAmount
        }, new JsonSerializerOptions { WriteIndented = true });
        invoice.TaxReportStatus = "Prepared";
        AppendAudit(invoice, employee, "Prepared", null, "Internal export prepared; no external transmission.");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return invoice.TaxReportJson;
    }
    public async Task RecordExternalResultAsync(long id, string status, string reference, string notes, ClaimsPrincipal user)
    {
        if (status is not ("Submitted externally" or "Accepted externally" or "Rejected externally"))
            throw new InvalidOperationException("Select a valid external result.");
        reference = reference.Trim(); notes = notes.Trim();
        if (reference.Length == 0 || reference.Length > 250 || notes.Length > 1000)
            throw new InvalidOperationException("Enter an external reference (1–250 characters); notes maximum 1000.");
        if (status == "Rejected externally" && notes.Length == 0) throw new InvalidOperationException("Enter the rejection reason.");
        await using var db = await factory.CreateDbContextAsync();
        var employee = await RequireAsync(db, user);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var invoice = await db.SalesInvoices.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new InvalidOperationException("Invoice not found.");
        if (invoice.Status != SalesInvoiceStatus.Issued || invoice.TaxReportJson == null)
            throw new InvalidOperationException("Prepare a valid reporting package first.");
        if (invoice.TaxReportStatus == "Accepted externally")
            throw new InvalidOperationException("Accepted invoices require an authority-compliant correction workflow, not status edits.");
        if (status != "Submitted externally" && invoice.TaxReportStatus != "Submitted externally")
            throw new InvalidOperationException("Record external submission before acceptance or rejection.");
        if (status == "Submitted externally" && invoice.TaxReportStatus is not ("Prepared" or "Rejected externally"))
            throw new InvalidOperationException("This report is already awaiting an external result.");
        invoice.TaxReportStatus = status;
        invoice.TaxAuthorityReference = reference;
        AppendAudit(invoice, employee, status, reference, notes);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
    public async Task<List<AuditEvent>> AuditAsync(long id, ClaimsPrincipal user)
    {
        await using var db = await factory.CreateDbContextAsync();
        await RequireAsync(db, user);
        var json = await db.SalesInvoices.Where(x => x.Id == id).Select(x => x.TaxReportAuditJson).FirstOrDefaultAsync();
        return string.IsNullOrEmpty(json) ? new() : JsonSerializer.Deserialize<List<AuditEvent>>(json) ?? new();
    }
    private static void AppendAudit(SalesInvoice invoice, string employee, string status, string? reference, string? notes)
    {
        var events = string.IsNullOrEmpty(invoice.TaxReportAuditJson) ? new List<AuditEvent>() :
            JsonSerializer.Deserialize<List<AuditEvent>>(invoice.TaxReportAuditJson) ?? new();
        events.Add(new(DateTime.UtcNow, employee, status, reference, notes));
        invoice.TaxReportAuditJson = JsonSerializer.Serialize(events);
    }
}
