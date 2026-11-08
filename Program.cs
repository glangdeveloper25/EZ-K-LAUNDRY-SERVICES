var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// --- DINAGDAG PARA GUMANA ANG LOGIN SESSION ---
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();

// "Login using Google" (Use Case). Google only proves the customer's email; the app then signs
// the customer in with its usual session. The short-lived cookie only carries the Google result.
var authentication = builder.Services
    .AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "EZK.ExternalLogin";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    });
string? googleClientId = builder.Configuration["Authentication:Google:ClientId"];
string? googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authentication.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}
// Staff-initiated SMS through the SMSGate (sms-gate.app) Android gateway (settings in the "Sms" section).
builder.Services.Configure<EZKLaundrySystem.Services.SmsOptions>(builder.Configuration.GetSection("Sms"));
builder.Services.AddHttpClient<EZKLaundrySystem.Services.SmsSender>(client => client.Timeout = TimeSpan.FromSeconds(15));
// Cancels online bookings when the customer does not arrive within the 15-minute grace period.
builder.Services.AddHostedService<EZKLaundrySystem.Services.GracePeriodCancellationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// I-COMMENT OUT ITO PARA HINDI MAG-ERROR SA LOCALHOST
// app.UseHttpsRedirection(); 

app.UseStaticFiles();
app.UseRouting();

// --- DAPAT NANDITO ANG USE SESSION ---
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
