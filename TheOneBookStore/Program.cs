using Microsoft.EntityFrameworkCore;

using TheOneBookStore.DataAccess;
using TheOneBookStore.DataAccess.Repository;
using TheOneBookStore.DataAccess.Repository.IRepository;
using Microsoft.AspNetCore.Identity;
using NuGet.Protocol.Core.Types;
using Microsoft.AspNetCore.Identity.UI.Services;
using TheOneBookStore.Utility;
using Stripe;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection")));
//options => options.SignIn.RequireConfirmedAccount = true
//builder.Services.AddDefaultIdentity<IdentityUser>()
//    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));   
builder.Services.AddIdentity<IdentityUser,IdentityRole>().AddDefaultTokenProviders()
    .AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultUI();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddMvc();
//builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
builder.Services.AddAuthentication().AddFacebook(options =>
{
    options.AppId = "6295907503755768";
    options.AppSecret = "e67b90ba10e4dc1bc0589e288dd965fb";
}
); 
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = $"/Identity/Account/Login";
    options.LogoutPath = $"/Identity/Account/Logout";
    options.AccessDeniedPath = $"/Identity/Account/AccessDenied";
});

//add session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(100);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
StripeConfiguration.ApiKey = builder.Configuration.GetSection("Stripe:SecretKey").Get<string>();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{area=Customer}/{controller=Home}/{action=Index}/{id?}");
app.Run();
