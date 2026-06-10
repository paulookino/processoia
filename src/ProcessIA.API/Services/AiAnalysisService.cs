using System.Text;
using System.Text.Json;
using ProcessIA.API.Models;

namespace ProcessIA.API.Services;

/// <summary>
/// Analisa documentos jurídicos usando Groq (se configurado) ou o analisador local.
/// Sem chave configurada: análise por padrões léxicos do direito brasileiro.
/// Com Groq__ApiKey: Llama 3.3 70B via Groq (grátis, 14.400 req/dia).
/// </summary>
public class AiAnalysisService(IConfiguration config, ILogger<AiAnalysisService> logger)
{
    private static readonly HttpClient Http = new();
    private readonly LegalDocumentAnalyzer _localAnalyzer = new();

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
        var groqKey = config["Groq:ApiKey"];

        if (string.IsNullOrWhiteSpace(groqKey))
        {
            logger.LogInformation("Groq key not configured — using local rule-based analyzer.");
            return _localAnalyzer.Analyze(extractedText);
        }

        logger.LogInformation("Using Groq (Llama 3.3 70B) for analysis.");
        var json = await CallGroqAsync(extractedText, groqKey);

        try
        {
            return ParseGroqResult(json);
        }
        catch (JsonException)
        {
            logger.LogWarning("Groq returned invalid JSON. Retrying.");
            var retry = await CallGroqAsync(
                $"Return ONLY a raw JSON object, no markdown, no explanation. Analyze:\n\n{extractedText}",
                groqKey);
            return ParseGroqResult(retry);
        }
    }

    private static async Task<string> CallGroqAsync(string userMessage, string apiKey)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = "llama-3.3-70b-versatile",
            max_tokens = 2048,
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = $"Analyze this legal process:\n\n{userMessage}" }
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;
    }

    private static AnalysisResult ParseGroqResult(string json)
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
