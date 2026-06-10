using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using ProcessIA.API.Models;

namespace ProcessIA.API.Services;

public class AiAnalysisService(IConfiguration config, ILogger<AiAnalysisService> logger)
{
    private readonly AnthropicClient _client = new(config["Anthropic:ApiKey"]!);

    private const string SystemPrompt = """
        You are a Brazilian legal document analyst with 20 years of experience.
        You have been given the full text of a scanned legal process (processo judicial).
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
        var messages = new List<Message>
        {
            new() { Role = RoleType.User, Content = $"Analyze this legal process:\n\n{extractedText}" }
        };

        var parameters = new MessageParameters
        {
            Model = AnthropicModels.Claude3Sonnet,
            MaxTokens = 2048,
            System = SystemPrompt,
            Messages = messages
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters);
        var json = response.Content.First().Text;

        try
        {
            return ParseResult(json);
        }
        catch (JsonException)
        {
            logger.LogWarning("Claude returned invalid JSON on first attempt. Retrying with stricter prompt.");
            return await RetryWithStricterPromptAsync(extractedText);
        }
    }

    private async Task<AnalysisResult> RetryWithStricterPromptAsync(string extractedText)
    {
        var messages = new List<Message>
        {
            new() {
                Role = RoleType.User,
                Content = $"Return ONLY a raw JSON object, no markdown, no explanation. Analyze:\n\n{extractedText}"
            }
        };

        var parameters = new MessageParameters
        {
            Model = AnthropicModels.Claude3Sonnet,
            MaxTokens = 2048,
            System = SystemPrompt,
            Messages = messages
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters);
        return ParseResult(response.Content.First().Text);
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
