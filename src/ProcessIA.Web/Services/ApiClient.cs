using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ProcessIA.Web.Services;

public class ApiClient(HttpClient http, IHttpContextAccessor ctx)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private void SetAuth()
    {
        var token = ctx.HttpContext?.Session.GetString("jwt");
        if (!string.IsNullOrEmpty(token))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<(bool ok, string? error)> LoginAsync(string email, string password)
    {
        var body = JsonSerializer.Serialize(new { email, password });
        var response = await http.PostAsync("/api/auth/login",
            new StringContent(body, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode) return (false, "E-mail ou senha inválidos.");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var token = doc.RootElement.GetProperty("token").GetString()!;

        ctx.HttpContext!.Session.SetString("jwt", token);
        ctx.HttpContext!.Session.SetString("email", email);
        return (true, null);
    }

    public async Task<(bool ok, string? error)> RegisterAsync(string email, string password)
    {
        var body = JsonSerializer.Serialize(new { email, password });
        var response = await http.PostAsync("/api/auth/register",
            new StringContent(body, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            return (false, "Erro ao criar conta. Verifique os dados.");
        }

        return (true, null);
    }

    public async Task<List<ProcessSummary>> GetProcessesAsync()
    {
        SetAuth();
        var response = await http.GetAsync("/api/processes");
        if (!response.IsSuccessStatusCode) return [];

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<ProcessSummary>>(json, JsonOpts) ?? [];
    }

    public async Task<(bool ok, Guid processId, string? error)> UploadAsync(IFormFile file)
    {
        SetAuth();
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        form.Add(content, "file", file.FileName);

        var response = await http.PostAsync("/api/processes/upload", form);

        if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
            return (false, Guid.Empty, "Assinatura necessária. Ative seu plano para continuar.");

        if (!response.IsSuccessStatusCode)
            return (false, Guid.Empty, "Erro ao enviar arquivo.");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var id = doc.RootElement.GetProperty("processId").GetGuid();
        return (true, id, null);
    }

    public async Task<ProcessStatus?> GetStatusAsync(Guid processId)
    {
        SetAuth();
        var response = await http.GetAsync($"/api/processes/{processId}/status");
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ProcessStatus>(json, JsonOpts);
    }

    public async Task<ReportDetail?> GetReportAsync(Guid processId)
    {
        SetAuth();
        var response = await http.GetAsync($"/api/reports/{processId}");
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ReportDetail>(json, JsonOpts);
    }

    public async Task<byte[]?> DownloadReportPdfAsync(Guid processId)
    {
        SetAuth();
        var response = await http.GetAsync($"/api/reports/{processId}/pdf");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsByteArrayAsync();
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(ctx.HttpContext?.Session.GetString("jwt"));
    public string? CurrentEmail => ctx.HttpContext?.Session.GetString("email");
    public void Logout() => ctx.HttpContext?.Session.Clear();
}

public record ProcessSummary(Guid Id, string FileName, long FileSizeBytes, string Status, DateTime UploadedAt, DateTime? ProcessedAt, bool HasReport);
public record ProcessStatus(Guid Id, string Status, string? ErrorMessage);
public record ReportDetail(Guid Id, Guid ProcessId, string FileName, JsonElement Parties, decimal? CaseValue, string CurrentPhase, string LastDecision, JsonElement NextDeadlines, string RiskLevel, string RiskJustification, DateTime CreatedAt);
