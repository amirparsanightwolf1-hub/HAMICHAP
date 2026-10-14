using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Customers;

public class Customer : BaseEntity
{
    public int Id { get; set; }


    public string Code { get; set; } = "";


    public string Name { get; set; } = "";


    public string Phone { get; set; } = "";


    public string Email { get; set; } = "";


    public string Address { get; set; } = "";


    public bool IsActive { get; set; } = true;
}