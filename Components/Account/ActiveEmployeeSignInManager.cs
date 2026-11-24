using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HamiChap_MES_ERP.Components.Account;

internal sealed class ActiveEmployeeSignInManager(
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
    public override Task<bool> CanSignInAsync(ApplicationUser user) =>
        user.IsActive ? base.CanSignInAsync(user) : Task.FromResult(false);
}
