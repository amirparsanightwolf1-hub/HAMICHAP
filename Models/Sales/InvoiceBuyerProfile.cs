using System.Text.Json;
using HamiChap_MES_ERP.Models.Customers;
using HamiChap_MES_ERP.Models.Sales.Enums;

namespace HamiChap_MES_ERP.Models.Sales;

public static class InvoiceTypeLabels
{
    public static string Label(SalesInvoiceType? type, bool persian) => type switch
    {
        SalesInvoiceType.Formal => persian ? "رسمی" : "Formal",
        SalesInvoiceType.Informal => persian ? "غیررسمی" : "Informal",
        _ => persian ? "قدیمی — نوع نامشخص" : "Legacy — unclassified"
    };
}

public sealed record InvoiceBuyerProfile(string Name, string IdentityLabel, string IdentityNumber,
    string EconomicCode, string RegistrationNumber, string Address, string PostalCode, string Phone)
{
    public static string Capture(Customer customer)
    {
        var natural = customer.CustomerType == CustomerType.Natural;
        var legal = customer.CustomerType == CustomerType.Legal;
        if ((!natural && !legal) || string.IsNullOrWhiteSpace(customer.Name) ||
            string.IsNullOrWhiteSpace(natural ? customer.NationalId : customer.NationalCode))
            throw new InvalidOperationException("Formal invoices require a customer profile with a name, customer type and national identification number. Complete the customer profile first.");
        return JsonSerializer.Serialize(new InvoiceBuyerProfile(
            customer.Name, natural ? "individual" : "company",
            natural ? customer.NationalId : customer.NationalCode,
            legal ? customer.EconomicCode : "", legal ? customer.RegistrationNumber : "",
            string.Join(" · ", new[] { customer.Province, customer.City, customer.Address }.Where(x => !string.IsNullOrWhiteSpace(x))),
            customer.PostalCode, string.IsNullOrWhiteSpace(customer.Phone) ? customer.Mobile : customer.Phone));
    }

    public static InvoiceBuyerProfile? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<InvoiceBuyerProfile>(json); }
        catch (JsonException) { return null; }
    }
}
