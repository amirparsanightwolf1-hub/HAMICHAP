using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser
{
    public string EmployeeNumber { get; set; } = "";

    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";

    public string Department { get; set; } = "";

    public string Position { get; set; } = "";

    public DateTime? HireDate { get; set; }

    public bool IsActive { get; set; } = true;
}