using HamiChap_MES_ERP.Components;
using HamiChap_MES_ERP.Components.Account;
using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Data.Seed;
using HamiChap_MES_ERP.Models.Shared;
using HamiChap_MES_ERP.Models.Shared.Services;
using HamiChap_MES_ERP.Services;
using HamiChap_MES_ERP.Services.Imposition;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<InventoryPostingService>();
builder.Services.AddScoped<WarehouseRequestService>();
builder.Services.AddScoped<EmployeeInboxService>();
builder.Services.AddScoped<MaterialSettlementService>();
builder.Services.AddScoped<ImpositionOptimizerService>();
builder.Services.AddScoped<StockBalanceService>();
builder.Services.AddScoped<SessionInactivityService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<IAuthorizationHandler, EmployeePermissionHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in new[]
    {
        "Inventory.Documents.View", "Inventory.Documents.Create",
        "Sales.Requests.View", "Sales.Requests.Create"
    })
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new EmployeePermissionRequirement(permission)));
});
builder.Services.AddScoped<LocalizationService>();
builder.Services.AddScoped<InventoryAlertService>();
builder.Services.AddScoped<SalesRequestService>();
builder.Services.AddScoped<SalesProofService>();
builder.Services.AddScoped<SalesEvaluationService>();
builder.Services.AddScoped<QuotationService>();
builder.Services.AddScoped<SalesOrderService>();
builder.Services.AddScoped<ProductionOrderService>();
builder.Services.AddScoped<BomService>();
builder.Services.AddScoped<MrpService>();
builder.Services.AddScoped<InventoryReservationService>();
builder.Services.AddScoped<ProductionExecutionService>();
builder.Services.AddScoped<ProductionInventoryService>();
builder.Services.AddScoped<ProductionPostingService>();
builder.Services.AddScoped<ProductionSchedulingService>();
builder.Services.AddScoped<ProductionQualityPostingService>();
builder.Services.AddScoped<ProductionOrderCompletionService>();
builder.Services.AddScoped<SalesDeliveryService>();
builder.Services.AddScoped<QualityInspectionService>();
builder.Services.AddScoped<SalesInvoiceService>();
builder.Services.AddScoped<ShipmentPaymentService>();
builder.Services.AddScoped<InvoiceTaxReportingService>();
builder.Services.AddScoped<ArtworkVectorizationService>();
builder.Services.AddScoped<ArtworkGeometryPreparationService>();
builder.Services.AddScoped<ArtworkRectangleDecompositionService>();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");;

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDbContext<ApplicationDbContext>(
    options => options.UseSqlServer(connectionString),
    contextLifetime: ServiceLifetime.Scoped,
    optionsLifetime: ServiceLifetime.Singleton);

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager<ActiveEmployeeSignInManager>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnValidatePrincipal = async context =>
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal?.Identity?.IsAuthenticated != true)
            return;
        var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.GetUserAsync(context.Principal) is not { IsActive: true })
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});

builder.Services.AddScoped<NumberSeriesService>();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

var supportedCultures = new[] { "en-US", "fa-IR" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Legacy upload URLs remain compatible with existing job records. Never serve
// them to unauthenticated visitors, even if a file is also a static web asset.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads"))
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        context.Response.Headers["Cache-Control"] = "private, no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }
    await next();
});

// PDF artwork previews are rendered locally by Docnet.Core (no external API/CDN).
app.MapLocalPdfPreview();

// Serve runtime-uploaded files only.
var uploadsPath = Path.Combine(
    app.Environment.ContentRootPath,
    "wwwroot",
    "uploads");

Directory.CreateDirectory(uploadsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

// Serve application assets and their compressed variants.
app.MapStaticAssets();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.MapGet("/Culture/Set", (HttpContext httpContext, string culture, string redirectUri) =>
{
    if (!string.IsNullOrEmpty(culture))
    {
        httpContext.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true }
        );
    }

    var safeRedirect = redirectUri;

    if (string.IsNullOrEmpty(safeRedirect) || !safeRedirect.StartsWith("/"))
    {
        safeRedirect = "/";
    }

    return Results.LocalRedirect(safeRedirect);
});

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<ApplicationUser>>();

    var context = scope.ServiceProvider
    .GetRequiredService<ApplicationDbContext>();

    await IdentitySeeder.SeedRolesAsync(roleManager);

    await IdentitySeeder.SeedAdminAsync(userManager, app.Configuration);

    await PermissionSeeder.SeedPermissionsAsync(context);

    await InventoryLocationSeeder.SeedAsync(context);

    await ProductionLocationSeeder.SeedAsync(context);

    await ProductionMachineSeeder.SeedAsync(context);
}

app.Run();
