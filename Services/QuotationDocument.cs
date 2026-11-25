using System.Text.Json;
using HamiChap_MES_ERP.Models.Customers;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using Microsoft.Extensions.Configuration;

namespace HamiChap_MES_ERP.Services;

public sealed record QuotationParty(string Name, string Address, string Phone, string Email,
    string IdentityNumber, string IdentityLabel, string EconomicCode, string RegistrationNumber, string PostalCode);

public sealed record QuotationDocumentParties(QuotationParty Seller, QuotationParty Buyer);

public static class QuotationDocument
{
    public static string Label(QuotationType? type, bool fa) => type switch
    {
        QuotationType.Formal => fa ? "پیش‌فاکتور رسمی" : "Formal quotation",
        QuotationType.Informal => fa ? "پیش‌فاکتور غیررسمی" : "Informal quotation",
        _ => fa ? "پیش‌فاکتور — نوع نامشخص" : "Quotation — unclassified"
    };

    public static SalesInvoiceType? InvoiceType(QuotationType? type) => type switch
    {
        QuotationType.Formal => SalesInvoiceType.Formal,
        QuotationType.Informal => SalesInvoiceType.Informal,
        _ => null
    };

    public static QuotationDocumentParties Capture(Quotation quote, Customer customer, IConfiguration config)
    {
        if (quote.QuotationType is not (QuotationType.Formal or QuotationType.Informal))
            throw new InvalidOperationException("Select formal or informal quotation before generating or sending it.");
        if (quote.Lines.Count == 0 || quote.Lines.Any(x => x.Quantity <= 0 || x.UnitPrice <= 0))
            throw new InvalidOperationException("Complete quotation quantities and pricing before generating it.");
        InvoiceVat.Validate(quote);
        if (quote.ValidUntil.HasValue && quote.ValidUntil.Value.Date < quote.QuotationDate.Date)
            throw new InvalidOperationException("Quotation validity date cannot precede its issue date.");
        var formal = quote.QuotationType == QuotationType.Formal;
        var seller = config.GetSection("TaxReporting");
        var branding = config.GetSection("QuotationBranding");
        string S(string key) => (seller[key] ?? "").Trim();
        string B(string key) => (branding[key] ?? "").Trim();
        string sellerName = S("LegalName");
        if (formal && (string.IsNullOrWhiteSpace(sellerName) || string.IsNullOrWhiteSpace(S("NationalId"))
            || string.IsNullOrWhiteSpace(S("EconomicCode")) || string.IsNullOrWhiteSpace(S("Address"))
            || string.IsNullOrWhiteSpace(S("PostalCode")) || S("CurrencyCode").ToUpperInvariant() != quote.CurrencyCode))
            throw new InvalidOperationException("Formal quotations require the configured seller legal name, national ID, economic code, address, postal code and matching currency in TaxReporting.");
        if (string.IsNullOrWhiteSpace(customer.Name))
            throw new InvalidOperationException("Complete the customer name before generating a quotation.");

        string address = string.Join(" · ", new[] { customer.Province, customer.City, customer.Address }.Where(x => !string.IsNullOrWhiteSpace(x)));
        string phone = string.IsNullOrWhiteSpace(customer.Phone) ? customer.Mobile : customer.Phone;
        bool legal = customer.CustomerType == CustomerType.Legal;
        string identity = legal ? customer.NationalCode : customer.NationalId;
        if (formal && (customer.CustomerType is not (CustomerType.Legal or CustomerType.Natural)
            || string.IsNullOrWhiteSpace(identity)))
            throw new InvalidOperationException("Formal quotations require the customer's individual or company national ID. Complete the customer profile first.");

        return new QuotationDocumentParties(
            new QuotationParty(formal ? sellerName : (B("DisplayName") is { Length: > 0 } name ? name : (sellerName.Length > 0 ? sellerName : "HamiChap")),
                S("Address"), B("Phone"), B("Email"), formal ? S("NationalId") : "", "company",
                formal ? S("EconomicCode") : "", formal ? S("RegistrationNumber") : "", formal ? S("PostalCode") : ""),
            new QuotationParty(customer.Name, address, phone, customer.Email, formal ? identity : "",
                legal ? "company" : "individual", formal && legal ? customer.EconomicCode : "",
                formal && legal ? customer.RegistrationNumber : "", formal ? customer.PostalCode : ""));
    }

    public static string Serialize(QuotationDocumentParties parties) => JsonSerializer.Serialize(parties);
    public static QuotationDocumentParties? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var result = JsonSerializer.Deserialize<QuotationDocumentParties>(json);
            return result is { Seller: not null, Buyer: not null } ? result : null;
        }
        catch (JsonException) { return null; }
    }
}
