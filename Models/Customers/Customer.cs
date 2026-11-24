using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Customers;

public enum CustomerType
{
    Unspecified = 0,
    Natural = 1,
    Legal = 2
}

public class Customer : BaseEntity
{
    public int Id { get; set; }

    public CustomerType CustomerType { get; set; }
    public string NationalId { get; set; } = "";
    public string NationalCode { get; set; } = "";
    public string EconomicCode { get; set; } = "";
    public string RegistrationNumber { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Province { get; set; } = "";
    public string City { get; set; } = "";
    public string ContactPerson { get; set; } = "";

    // Copy only profile data; retain the ERP code, identity and status of existing customers.
    public void CopyProfileTo(Customer target)
    {
        target.CustomerType = CustomerType;
        target.Name = Name.Trim();
        target.Phone = Phone.Trim();
        target.Email = Email.Trim();
        target.Address = Address.Trim();
        target.NationalId = CustomerType == global::HamiChap_MES_ERP.Models.Customers.CustomerType.Natural ? NationalId.Trim() : "";
        target.NationalCode = CustomerType == global::HamiChap_MES_ERP.Models.Customers.CustomerType.Legal ? NationalCode.Trim() : "";
        target.EconomicCode = CustomerType == global::HamiChap_MES_ERP.Models.Customers.CustomerType.Legal ? EconomicCode.Trim() : "";
        target.RegistrationNumber = CustomerType == global::HamiChap_MES_ERP.Models.Customers.CustomerType.Legal ? RegistrationNumber.Trim() : "";
        target.Mobile = Mobile.Trim();
        target.PostalCode = PostalCode.Trim();
        target.Province = Province.Trim();
        target.City = City.Trim();
        target.ContactPerson = CustomerType == global::HamiChap_MES_ERP.Models.Customers.CustomerType.Legal ? ContactPerson.Trim() : "";
    }


    public string Code { get; set; } = "";


    public string Name { get; set; } = "";


    public string Phone { get; set; } = "";


    public string Email { get; set; } = "";


    public string Address { get; set; } = "";


    public bool IsActive { get; set; } = true;
}