using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Data.Seeders;
using Staybnb.Models.Entities;
using Staybnb.Models.Settings;
using Staybnb.Services;
using Staybnb.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// 1. DATABASE (EF Core + SQL Server)
// =========================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// =========================================================================
// 2. IDENTITY (users + roles)
//    - ApplicationUser instead of the default IdentityUser
//    - AddRoles<IdentityRole>() enables Guest / Host / Admin / SuperAdmin
// =========================================================================
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        // No email server in this project, so don't require email confirmation
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Re-read the user's roles from the database on every request.
// This makes the Guest -> Host switch take effect immediately after an Admin
// approves the application, without the user having to log out and in again.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    // Our own AccountController (Part 2) instead of the built-in Identity pages
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// =========================================================================
// 3. MVC + RAZOR PAGES (Razor Pages are used by the built-in Identity UI)
// =========================================================================
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// =========================================================================
// 4. APPLICATION SERVICES (Dependency Injection)
//    AddScoped = one instance per HTTP request (same lifetime as the DbContext)
// =========================================================================
builder.Services.Configure<StaybnbSettings>(builder.Configuration.GetSection("Staybnb"));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IHostApplicationService, HostApplicationService>();
builder.Services.AddScoped<ICheckInService, CheckInService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

// Uploads: a host may send up to 10 photos x 5 MB in one form.
// Kestrel's default request limit is about 30 MB, so raise it to 60 MB.
const long MaxUploadBytes = 60L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxUploadBytes);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    options.MultipartBodyLengthLimit = MaxUploadBytes);

var app = builder.Build();

// =========================================================================
// CULTURE: South African formatting, but with "." as the decimal separator.
// Windows' en-ZA uses "," for decimals, which makes "1500.50" fail model binding
// and clash with HTML number inputs / jQuery validation.
// =========================================================================
var staybnbCulture = (CultureInfo)CultureInfo.GetCultureInfo("en-ZA").Clone();
staybnbCulture.NumberFormat.NumberDecimalSeparator = ".";
staybnbCulture.NumberFormat.CurrencyDecimalSeparator = ".";
staybnbCulture.NumberFormat.NumberGroupSeparator = " ";
staybnbCulture.NumberFormat.CurrencyGroupSeparator = " ";
staybnbCulture.NumberFormat.CurrencySymbol = "R";
CultureInfo.DefaultThreadCurrentCulture = staybnbCulture;
CultureInfo.DefaultThreadCurrentUICulture = staybnbCulture;
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(staybnbCulture),
    SupportedCultures = new List<CultureInfo> { staybnbCulture },
    SupportedUICultures = new List<CultureInfo> { staybnbCulture }
});

// =========================================================================
// 5. HTTP REQUEST PIPELINE (order matters!)
// =========================================================================
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// The built-in Identity Register/Login pages would skip our "assign Guest role" and
// activity-logging logic, so send anyone who reaches them to our AccountController.
app.UseRewriter(new RewriteOptions()
    .AddRedirect(@"^Identity/Account/(Register|Login|AccessDenied)$", "Account/$1"));

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();   // who are you?   (must come first)
app.UseAuthorization();    // what may you do?

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// =========================================================================
// 6. SEED: apply migrations, create roles + the SuperAdmin account
// =========================================================================
await RoleSeeder.SeedAsync(app.Services);

app.Run();
