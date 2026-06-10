using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProcessIA.API.Data;
using ProcessIA.API.Models;
using ProcessIA.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Database — converte URI do Render para formato Npgsql se necessário
var rawConnStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
var connStr = NormalizeConnectionString(rawConnStr);

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connStr));

// Identity
builder.Services.AddIdentity<User, IdentityRole>(opt =>
{
    opt.Password.RequireDigit = true;
    opt.Password.RequiredLength = 8;
    opt.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// JWT — deve vir APÓS AddIdentity para sobrescrever DefaultChallengeScheme (cookies → JWT)
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultScheme             = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});

builder.Services.AddAuthorization();

// Hangfire
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(connStr));
builder.Services.AddHangfireServer();

// App Services
builder.Services.AddSingleton<TempFileService>();
builder.Services.AddScoped<PdfTextExtractor>();
builder.Services.AddScoped<AiAnalysisService>();
builder.Services.AddScoped<ProcessingPipeline>();

builder.Services.AddControllers();

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Landing page
app.MapGet("/", () => Results.Content("""
<!DOCTYPE html>
<html lang="pt-BR">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>ProcessIA — API</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background: #0f1117; color: #e2e8f0; min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px; }
    .card { background: #1a1d27; border: 1px solid #2d3148; border-radius: 16px; padding: 48px; max-width: 560px; width: 100%; }
    .badge { display: inline-flex; align-items: center; gap: 8px; background: #0d2d1a; border: 1px solid #1a5c35; color: #4ade80; border-radius: 99px; padding: 6px 14px; font-size: 0.8rem; font-weight: 600; margin-bottom: 24px; }
    .dot { width: 8px; height: 8px; background: #4ade80; border-radius: 50%; animation: pulse 2s infinite; }
    @keyframes pulse { 0%,100%{opacity:1} 50%{opacity:.4} }
    h1 { font-size: 2rem; font-weight: 700; color: #f8fafc; margin-bottom: 8px; }
    .sub { color: #94a3b8; font-size: 0.95rem; margin-bottom: 32px; line-height: 1.6; }
    .endpoints { border-top: 1px solid #2d3148; padding-top: 24px; margin-bottom: 32px; }
    .endpoints h2 { font-size: 0.75rem; text-transform: uppercase; letter-spacing: .1em; color: #64748b; margin-bottom: 16px; }
    .ep { display: flex; align-items: center; gap: 12px; margin-bottom: 10px; font-size: 0.875rem; }
    .method { font-size: 0.7rem; font-weight: 700; padding: 2px 8px; border-radius: 4px; min-width: 44px; text-align: center; }
    .post { background: #1a3a1a; color: #4ade80; }
    .get { background: #1a2d4a; color: #60a5fa; }
    code { color: #cbd5e1; font-family: 'Courier New', monospace; font-size: 0.82rem; }
    .stack { display: flex; gap: 8px; flex-wrap: wrap; }
    .tag { background: #1e2235; border: 1px solid #2d3148; color: #94a3b8; padding: 4px 12px; border-radius: 6px; font-size: 0.78rem; }
    a.repo { display: inline-flex; align-items: center; gap: 6px; color: #60a5fa; text-decoration: none; font-size: 0.875rem; margin-top: 24px; border-top: 1px solid #2d3148; padding-top: 20px; }
    a.repo:hover { color: #93c5fd; }
  </style>
</head>
<body>
  <div class="card">
    <div class="badge"><span class="dot"></span> API online</div>
    <h1>ProcessIA</h1>
    <p class="sub">Análise inteligente de documentos jurídicos.<br>Envie um PDF, receba um relatório estruturado em minutos.</p>

    <div class="endpoints">
      <h2>Endpoints</h2>
      <div class="ep"><span class="method post">POST</span><code>/api/auth/register</code></div>
      <div class="ep"><span class="method post">POST</span><code>/api/auth/login</code></div>
      <div class="ep"><span class="method post">POST</span><code>/api/processes</code></div>
      <div class="ep"><span class="method get">GET</span><code>/api/reports/{id}</code></div>
      <div class="ep"><span class="method get">GET</span><code>/api/reports/{id}/pdf</code></div>
      <div class="ep"><span class="method get">GET</span><code>/health</code></div>
    </div>

    <div class="stack">
      <span class="tag">ASP.NET Core 8</span>
      <span class="tag">PostgreSQL</span>
      <span class="tag">EF Core</span>
      <span class="tag">Hangfire</span>
      <span class="tag">Docker</span>
    </div>

    <a class="repo" href="https://github.com/paulookino/processoia" target="_blank">
      ↗ Ver código no GitHub
    </a>
  </div>
</body>
</html>
""", "text/html"));

// Migrations com retry
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    for (int attempt = 1; attempt <= 5; attempt++)
    {
        try
        {
            logger.LogInformation("Applying migrations (attempt {Attempt}/5)...", attempt);
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Migrations applied successfully.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Migration attempt {Attempt} failed.", attempt);
            if (attempt == 5)
                logger.LogError("All migration attempts failed. App starting without migrations.");
            else
                await Task.Delay(TimeSpan.FromSeconds(attempt * 3));
        }
    }
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    IsReadOnlyFunc = _ => true
});

app.MapControllers();
app.Run();

// Converte URI postgresql:// para formato chave=valor do Npgsql
// e garante que a rede interna do Render não exija SSL
static string NormalizeConnectionString(string input)
{
    if (!input.StartsWith("postgres://") && !input.StartsWith("postgresql://"))
        return input;

    var uri = new Uri(input);
    var userInfo = uri.UserInfo.Split(':');
    var user = Uri.UnescapeDataString(userInfo[0]);
    var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var db = uri.AbsolutePath.TrimStart('/');

    return $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Disable;Trust Server Certificate=true";
}
