using System.Security.Claims;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.EntityFrameworkCore;
namespace HamiChap_MES_ERP.Services;

public sealed class ShipmentPaymentService(IDbContextFactory<ApplicationDbContext> factory, PermissionService permissions, Microsoft.Extensions.Configuration.IConfiguration configuration)
{
    private async Task<string> RequireAsync(ClaimsPrincipal user, string permission)
    {
        if (!await permissions.HasPermissionAsync(user, permission)) throw new UnauthorizedAccessException("Required permission missing.");
        return user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
    }
    public async Task<long> PrepareInvoiceAsync(long deliveryId, ClaimsPrincipal user, SalesInvoiceType invoiceType)
    {
        if (!Enum.IsDefined(invoiceType)) throw new InvalidOperationException("Select a valid invoice type.");
        var employee = await RequireAsync(user, "Sales.Deliveries.Create");
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (!await db.Users.AnyAsync(x => x.Id == employee && x.IsActive)) throw new UnauthorizedAccessException();
        var existing = await db.SalesInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.SalesDeliveryId == deliveryId);
        if (existing != null)
        {
            if (existing.InvoiceType != invoiceType) throw new InvalidOperationException("An invoice has already been issued with a different or unclassified type.");
            return existing.Id;
        }
        var quotation = await db.SalesDeliveries.AsNoTracking().Where(x => x.Id == deliveryId)
            .Select(x => x.SalesOrder.Quotation).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Delivery or quotation not found.");
        TaxSellerProfile seller;
        if (quotation.QuotationType == QuotationType.Formal && quotation.SellerCompanyId is int companyId)
        {
            seller = QuotationDocument.Read(quotation.DocumentPartiesJson)?.TaxSeller
                ?? throw new InvalidOperationException("The accepted formal quotation is missing its seller snapshot.");
            // A tax memory ID can be supplied later, but only for the exact same
            // registered issuer. Never replace the accepted legal party details.
            if (seller.Jurisdiction == "IR" && string.IsNullOrWhiteSpace(seller.TaxMemoryId))
            {
                var current = await db.SellerCompanies.AsNoTracking().SingleAsync(x => x.Id == companyId);
                if (current.Jurisdiction == seller.Jurisdiction && current.NationalId == seller.NationalId &&
                    current.CurrencyCode == seller.CurrencyCode)
                    seller = seller with { TaxMemoryId = current.TaxMemoryId };
            }
            seller.ValidateForInvoice();
        }
        else
        {
            seller = TaxSellerProfile.FromConfiguration(configuration);
            var previousSeller = QuotationDocument.Read(quotation.DocumentPartiesJson)?.Seller;
            if (quotation.QuotationType == QuotationType.Formal && previousSeller is not null &&
                previousSeller.IdentityNumber != seller.NationalId)
                throw new InvalidOperationException("Configured invoice seller does not match the accepted formal quotation.");
        }
        var invoice = await new SalesInvoiceService(db, new NumberSeriesService(db)).CreateForDeliveryAsync(deliveryId, employee, invoiceType, seller);
        var recipients = await EmployeeInboxEvents.RecipientsAsync(db, "Sales.Invoices.ConfirmPayment");
        EmployeeInboxEvents.Add(db, recipients, EmployeeInboxEvents.InvoiceReady, invoice.Id,
            "Invoice awaiting payment: " + invoice.InvoiceNumber, "Verify receipt of the full invoice amount.",
            "فاکتور در انتظار پرداخت: " + invoice.InvoiceNumber, "دریافت کل مبلغ فاکتور را بررسی کنید.", employee);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return invoice.Id;
    }
    public async Task ConfirmPaymentAsync(long invoiceId, string reference, ClaimsPrincipal user)
    {
        var employee = await RequireAsync(user, "Sales.Invoices.ConfirmPayment");
        reference = reference.Trim();
        if (reference.Length == 0 || reference.Length > 500) throw new InvalidOperationException("Enter a payment reference (1–500 characters).");
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (!await db.Users.AnyAsync(x => x.Id == employee && x.IsActive)) throw new UnauthorizedAccessException();
        var invoice = await db.SalesInvoices.Include(x => x.SalesDelivery).FirstOrDefaultAsync(x => x.Id == invoiceId)
            ?? throw new InvalidOperationException("Invoice not found.");
        if (invoice.Status != SalesInvoiceStatus.Issued || invoice.SalesDelivery.Status != SalesDeliveryStatus.Draft)
            throw new InvalidOperationException("Only issued invoices awaiting shipment can be confirmed.");
        if (invoice.PaidAtUtc.HasValue) return;
        invoice.PaidAtUtc = DateTime.UtcNow;
        invoice.PaidByUserId = employee;
        invoice.PaymentReference = reference;
        var recipients = await EmployeeInboxEvents.RecipientsAsync(db, "Sales.Deliveries.View", "Sales.Deliveries.Post");
        EmployeeInboxEvents.Add(db, recipients, EmployeeInboxEvents.ShipmentReady, invoice.Id,
            "Paid invoice: " + invoice.InvoiceNumber, "Payment confirmed. Check the shipment queue for dispatch.",
            "فاکتور پرداخت شده: " + invoice.InvoiceNumber, "پرداخت تأیید شد. صف ارسال را بررسی کنید.", employee);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
    public async Task DispatchAsync(long deliveryId, ClaimsPrincipal user)
    {
        var employee = await RequireAsync(user, "Sales.Deliveries.Post");
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Users.AnyAsync(x => x.Id == employee && x.IsActive)) throw new UnauthorizedAccessException();
        var numbering = new NumberSeriesService(db);
        await new SalesDeliveryService(db, new InventoryPostingService(db), numbering, new SalesInvoiceService(db, numbering))
            .PostDeliveryAsync(deliveryId, employee);
    }
}
