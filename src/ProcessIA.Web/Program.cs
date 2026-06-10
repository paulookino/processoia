using ProcessIA.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSession(opt =>
{
    opt.Cookie.HttpOnly = true;
    opt.Cookie.IsEssential = true;
    opt.IdleTimeout = TimeSpan.FromDays(7);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]!);
});

var app = builder.Build();

app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.MapRazorPages();

app.Run();
