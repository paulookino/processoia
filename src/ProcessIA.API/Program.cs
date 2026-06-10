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

// JWT
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

// Health check — Render usa isso pra saber se a app está up
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

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
