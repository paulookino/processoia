using System.Text.Json;

namespace ProcessIA.API.Models;

public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProcessId { get; set; }
    public LegalProcess Process { get; set; } = null!;

    public JsonDocument Parties { get; set; } = null!;
    public decimal? CaseValue { get; set; }
    public string CurrentPhase { get; set; } = null!;
    public string LastDecision { get; set; } = null!;
    public JsonDocument NextDeadlines { get; set; } = null!;
    public RiskLevel RiskLevel { get; set; }
    public string RiskJustification { get; set; } = null!;

    public string RawExtractedText { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum RiskLevel
{
    Low,
    Medium,
    High
}
