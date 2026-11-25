using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

// Shared reusable directory of legal sellers. Never store signing keys or credentials here.
public sealed class SellerCompany : BaseEntity
{
    public int Id { get; set; }
    public string LegalName { get; set; } = "";
    public string NationalId { get; set; } = "";
    public string EconomicCode { get; set; } = "";
    public string RegistrationNumber { get; set; } = "";
    public string Address { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Jurisdiction { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public string TaxMemoryId { get; set; } = "";
}
