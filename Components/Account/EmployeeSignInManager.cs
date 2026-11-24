using HamiChap_MES_ERP.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HamiChap_MES_ERP.Components.Account;

public sealed class EmployeeSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory,
        optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(ApplicationUser user)
        => user.IsActive && await base.CanSignInAsync(user);
}
