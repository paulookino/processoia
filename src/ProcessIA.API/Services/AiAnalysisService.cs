using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ProcessIA.API.Models;

namespace ProcessIA.API.Services;

public class AiAnalysisService(IConfiguration config, ILogger<AiAnalysisService> logger)
{
    private static readonly HttpClient Http = new();

    private const string SystemPrompt = """
        You are a Brazilian legal document analyst with 20 years of experience.
        You have been given the full text of a legal process (processo judicial).
        The text may have OCR errors — use context to interpret correctly.

        Extract and return ONLY valid JSON with this exact structure:

        {
          "parties": {
            "plaintiff": "name of the autor/requerente",
            "defendant": "name of the réu/requerido",
            "plaintiff_lawyer": "name if found, null otherwise",
            "defendant_lawyer": "name if found, null otherwise"
          },
          "case_value": 0.00,
          "current_phase": "brief description in Portuguese",
          "last_decision": "clear summary in plain Portuguese, no legalese, max 3 sentences",
          "next_deadlines": ["deadline 1", "deadline 2"],
          "risk_level": "LOW | MEDIUM | HIGH",
          "risk_justification": "one sentence explaining the risk classification"
        }

        Rules:
        - If you cannot find a field, use null. Never hallucinate.
        - last_decision must be written so a non-lawyer understands it.
        - risk_level: HIGH if there's likely condemnation, MEDIUM if uncertain, LOW if favorable.
        - Return ONLY the JSON. No explanation, no markdown, no preamble.
        """;

    public async Task<AnalysisResult> AnalyzeAsync(string extractedText)
    {
        var json = await CallClaudeAsync($"Analyze this legal process:\n\n{extractedText}");

        try
        {
            return ParseResult(json);
        }
        catch (JsonException)
        {
            logger.LogWarning("Claude returned invalid JSON. Retrying.");
            var retry = await CallClaudeAsync(
                $"Return ONLY a raw JSON object, no markdown, no explanation. Analyze:\n\n{extractedText}");
            return ParseResult(retry);
        }
    }

    private async Task<string> CallClaudeAsync(string userMessage)
    {
        var apiKey = config["Anthropic:ApiKey"]!;

        var body = JsonSerializer.Serialize(new
        {
            model = "claude-haiku-4-5-20251001",
            max_tokens = 2048,
            system = SystemPrompt,
            messages = new[]
            {
                new { role = "user", content = userMessage }
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString()!;
    }

    private static AnalysisResult ParseResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new AnalysisResult
        {
            Parties = JsonDocument.Parse(root.GetProperty("parties").GetRawText()),
            CaseValue = root.TryGetProperty("case_value", out var cv) && cv.ValueKind != JsonValueKind.Null
                ? cv.GetDecimal() : null,
            CurrentPhase = root.GetProperty("current_phase").GetString() ?? string.Empty,
            LastDecision = root.GetProperty("last_decision").GetString() ?? string.Empty,
            NextDeadlines = JsonDocument.Parse(root.GetProperty("next_deadlines").GetRawText()),
            RiskLevel = Enum.Parse<RiskLevel>(root.GetProperty("risk_level").GetString()!, ignoreCase: true),
            RiskJustification = root.GetProperty("risk_justification").GetString() ?? string.Empty
        };
    }
}

public record AnalysisResult
{
    public JsonDocument Parties { get; init; } = null!;
    public decimal? CaseValue { get; init; }
    public string CurrentPhase { get; init; } = null!;
    public string LastDecision { get; init; } = null!;
    public JsonDocument NextDeadlines { get; init; } = null!;
    public RiskLevel RiskLevel { get; init; }
    public string RiskJustification { get; init; } = null!;
}
