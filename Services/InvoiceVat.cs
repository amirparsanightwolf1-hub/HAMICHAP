using System.Text.Json;
using HamiChap_MES_ERP.Models.Sales;
using Microsoft.Extensions.Configuration;

namespace HamiChap_MES_ERP.Services;

public static class InvoiceVat
{
    // Amounts are tax-exclusive. Existing accepted quotations without VAT
    // metadata must never have tax silently added after customer acceptance.
    public static decimal Round(decimal value, string? currency) =>
        decimal.Round(value, currency == "IRR" ? 0 : 2, MidpointRounding.AwayFromZero);
    public static decimal Net(QuotationLine line, string? currency) => Round(line.Quantity * line.UnitPrice, currency);
    public static decimal Tax(QuotationLine line, string? currency) => Round(Net(line, currency) * (line.VatRate ?? 0m) / 100m, currency);
    public static decimal Subtotal(Quotation quote) => quote.Lines.Sum(x => Net(x, quote.CurrencyCode));
    public static decimal TotalTax(Quotation quote) => quote.Lines.Sum(x => Tax(x, quote.CurrencyCode));
    public static decimal Gross(Quotation quote) => string.IsNullOrWhiteSpace(quote.CurrencyCode) && quote.Lines.All(x => x.VatRate == null)
        ? quote.Lines.Sum(x => x.TotalPrice) : Subtotal(quote) + TotalTax(quote);

    public static void Validate(Quotation quote)
    {
        if (quote.CurrencyCode is not { Length: 3 } currency || !currency.All(x => x >= 'A' && x <= 'Z'))
            throw new InvalidOperationException("Select the actual invoice currency (three uppercase ISO currency letters). Do not enter toman amounts as IRR.");
        if (quote.Lines.Count == 0) throw new InvalidOperationException("Invoice lines required.");
        foreach (var line in quote.Lines)
        {
            if (line.VatRate is not { } rate || rate < 0 || rate > 100 || decimal.Round(rate, 2) != rate)
                throw new InvalidOperationException("Enter a verified VAT rate (0–100%, up to two decimal places) for each line.");
            if (string.IsNullOrWhiteSpace(line.TaxItemCode) || line.TaxItemCode.Trim().Length > 64)
                throw new InvalidOperationException("Enter the official goods/service identifier for every line.");
            if (rate == 0 && string.IsNullOrWhiteSpace(line.TaxTreatmentReason))
                throw new InvalidOperationException("Zero VAT requires an exemption or zero-rating justification.");
            if ((line.TaxTreatmentReason?.Length ?? 0) > 250)
                throw new InvalidOperationException("VAT justification must be no more than 250 characters.");
        }
    }

    // Difference of rounded cumulative amounts allocates the exact agreed
    // VAT across partial shipments, including the rounding remainder.
    public static (decimal Net, decimal Vat) Allocate(QuotationLine line, decimal previous, decimal quantity, string currency)
    {
        decimal Base(decimal units) => Round(units * line.UnitPrice, currency);
        decimal Vat(decimal units) => Round(Base(units) * line.VatRate!.Value / 100m, currency);
        return (Base(previous + quantity) - Base(previous), Vat(previous + quantity) - Vat(previous));
    }
}

public sealed record TaxSellerProfile(string Jurisdiction, string CurrencyCode, string LegalName, string NationalId,
    string EconomicCode, string RegistrationNumber, string Address, string PostalCode, string TaxMemoryId)
{
    public static TaxSellerProfile FromConfiguration(IConfiguration configuration)
    {
        var s = configuration.GetSection("TaxReporting");
        string Get(string name) => (s[name] ?? "").Trim();
        var profile = new TaxSellerProfile(Get("Jurisdiction").ToUpperInvariant(), Get("CurrencyCode").ToUpperInvariant(),
            Get("LegalName"), Get("NationalId"), Get("EconomicCode"), Get("RegistrationNumber"),
            Get("Address"), Get("PostalCode"), Get("TaxMemoryId"));
        if (profile.Jurisdiction.Length != 2 || profile.CurrencyCode.Length != 3 ||
            string.IsNullOrWhiteSpace(profile.LegalName) || string.IsNullOrWhiteSpace(profile.NationalId) ||
            string.IsNullOrWhiteSpace(profile.EconomicCode) || string.IsNullOrWhiteSpace(profile.Address) ||
            string.IsNullOrWhiteSpace(profile.PostalCode))
            throw new InvalidOperationException("Configure the registered seller, jurisdiction, currency and tax identification in TaxReporting before issuing new invoices.");
        return profile;
    }
    public static TaxSellerProfile? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<TaxSellerProfile>(json); } catch (JsonException) { return null; }
    }
}
