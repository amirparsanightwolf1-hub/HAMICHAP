using System.Text.Json;
using HamiChap_MES_ERP.Models.Customers;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;
using Microsoft.Extensions.Configuration;

namespace HamiChap_MES_ERP.Services;

public sealed record QuotationParty(string Name, string Address, string Phone, string Email,
    string IdentityNumber, string IdentityLabel, string EconomicCode, string RegistrationNumber, string PostalCode);

public sealed record QuotationDocumentParties(QuotationParty Seller, QuotationParty Buyer, TaxSellerProfile? TaxSeller = null);

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

    public static QuotationDocumentParties Capture(Quotation quote, Customer customer, IConfiguration config, SellerCompany? company = null)
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
        if (formal)
        {
            if (company is null || quote.SellerCompanyId != company.Id)
                throw new InvalidOperationException("Select or save a seller company before generating a formal quotation.");
            ValidateSeller(company);
            if (company.CurrencyCode != quote.CurrencyCode)
                throw new InvalidOperationException("The quotation currency must match the selected seller company's currency.");
        }
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
            formal
                ? new QuotationParty(company!.LegalName, company.Address, company.Phone, company.Email,
                    company.NationalId, "company", company.EconomicCode, company.RegistrationNumber, company.PostalCode)
                : new QuotationParty(B("DisplayName") is { Length: > 0 } name ? name : (sellerName.Length > 0 ? sellerName : "HamiChap"),
                    S("Address"), B("Phone"), B("Email"), "", "company", "", "", ""),
            new QuotationParty(customer.Name, address, phone, customer.Email, formal ? identity : "",
                legal ? "company" : "individual", formal && legal ? customer.EconomicCode : "",
                formal && legal ? customer.RegistrationNumber : "", formal ? customer.PostalCode : ""),
            formal ? ToTaxSeller(company!) : null);
    }

    public static void NormalizeSeller(SellerCompany company)
    {
        company.LegalName = (company.LegalName ?? "").Trim();
        company.NationalId = (company.NationalId ?? "").Trim();
        company.EconomicCode = (company.EconomicCode ?? "").Trim();
        company.RegistrationNumber = (company.RegistrationNumber ?? "").Trim();
        company.Address = (company.Address ?? "").Trim();
        company.PostalCode = (company.PostalCode ?? "").Trim();
        company.Phone = (company.Phone ?? "").Trim();
        company.Email = (company.Email ?? "").Trim();
        company.Jurisdiction = (company.Jurisdiction ?? "").Trim().ToUpperInvariant();
        company.CurrencyCode = (company.CurrencyCode ?? "").Trim().ToUpperInvariant();
        company.TaxMemoryId = (company.TaxMemoryId ?? "").Trim();
        // Persian and Arabic digits must not create duplicate national IDs.
        company.NationalId = NormalizeDigits(company.NationalId);
        company.EconomicCode = NormalizeDigits(company.EconomicCode);
        company.PostalCode = NormalizeDigits(company.PostalCode);
    }

    private static string NormalizeDigits(string value) => string.Concat(value.Select(c =>
        c is >= '\u06F0' and <= '\u06F9' ? (char)('0' + c - '\u06F0') :
        c is >= '\u0660' and <= '\u0669' ? (char)('0' + c - '\u0660') : c));

    public static void ValidateSeller(SellerCompany company)
    {
        if (string.IsNullOrWhiteSpace(company.LegalName) || string.IsNullOrWhiteSpace(company.NationalId)
            || string.IsNullOrWhiteSpace(company.EconomicCode) || string.IsNullOrWhiteSpace(company.Address)
            || string.IsNullOrWhiteSpace(company.PostalCode))
            throw new InvalidOperationException("Enter the seller company legal name, national ID, economic code, address and postal code.");
        if (company.Jurisdiction.Length != 2 || !company.Jurisdiction.All(c => c >= 'A' && c <= 'Z')
            || company.CurrencyCode.Length != 3 || !company.CurrencyCode.All(c => c >= 'A' && c <= 'Z'))
            throw new InvalidOperationException("Enter a two-letter jurisdiction and three-letter ISO currency code.");
        if (company.LegalName.Length > 200
            || company.NationalId.Length > 64
            || company.EconomicCode.Length > 64
            || company.RegistrationNumber.Length > 64
            || company.Address.Length > 600
            || company.PostalCode.Length > 32
            || company.Phone.Length > 64
            || company.Email.Length > 254
            || company.Jurisdiction.Length > 2
            || company.CurrencyCode.Length > 3
            || company.TaxMemoryId.Length > 100)
            throw new InvalidOperationException("One or more seller company details exceed the permitted length.");
        if (company.Email.Length > 0 && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(company.Email))
            throw new InvalidOperationException("Enter a valid seller email address.");
    }

    public static TaxSellerProfile ToTaxSeller(SellerCompany company) => new(company.Jurisdiction, company.CurrencyCode,
        company.LegalName, company.NationalId, company.EconomicCode, company.RegistrationNumber,
        company.Address, company.PostalCode, company.TaxMemoryId);

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
