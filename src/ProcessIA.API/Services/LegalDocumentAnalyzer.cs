using System.Text.Json;
using System.Text.RegularExpressions;
using ProcessIA.API.Models;

namespace ProcessIA.API.Services;

/// <summary>
/// Extrai estruturadamente informações de processos judiciais brasileiros
/// usando padrões léxicos e contextuais do direito processual civil/trabalhista.
/// </summary>
public class LegalDocumentAnalyzer
{
    private static readonly string[] PlaintiffMarkers =
        ["Autor:", "Autora:", "Requerente:", "Reclamante:", "Apelante:", "Exequente:", "Impetrante:", "AUTOR:", "AUTORA:", "REQUERENTE:", "RECLAMANTE:"];

    private static readonly string[] DefendantMarkers =
        ["Réu:", "Ré:", "Requerido:", "Requerida:", "Reclamado:", "Reclamada:", "Apelado:", "Apelada:", "Executado:", "RÉU:", "RÉ:", "REQUERIDO:", "RECLAMADO:"];

    private static readonly string[] LawyerMarkers =
        ["Advogado:", "Advogada:", "Dr.", "Dra.", "OAB", "patrono"];

    private static readonly Dictionary<string, string> PhaseKeywords = new()
    {
        ["sentença"] = "Sentença proferida",
        ["acórdão"] = "Acórdão — fase recursal",
        ["audiência de instrução"] = "Instrução processual",
        ["audiência de conciliação"] = "Tentativa de conciliação",
        ["citação"] = "Fase de citação",
        ["contestação"] = "Fase de defesa",
        ["réplica"] = "Fase de réplica",
        ["perícia"] = "Perícia em andamento",
        ["recurso"] = "Fase recursal",
        ["apelação"] = "Apelação",
        ["execução"] = "Fase de execução",
        ["cumprimento de sentença"] = "Cumprimento de sentença",
        ["embargos"] = "Embargos opostos",
        ["liquidação"] = "Liquidação de sentença",
        ["despacho"] = "Despacho recente",
    };

    private static readonly Dictionary<string, RiskLevel> RiskKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["procedente"] = RiskLevel.Low,
        ["parcialmente procedente"] = RiskLevel.Medium,
        ["improcedente"] = RiskLevel.High,
        ["condenou"] = RiskLevel.High,
        ["absolveu"] = RiskLevel.Low,
        ["extinto sem resolução"] = RiskLevel.Medium,
        ["extinto com resolução"] = RiskLevel.Low,
        ["provido"] = RiskLevel.Low,
        ["desprovido"] = RiskLevel.High,
        ["não provido"] = RiskLevel.High,
    };

    public AnalysisResult Analyze(string text)
    {
        var (plaintiff, defendant) = ExtractParties(text);
        var (plaintiffLawyer, defendantLawyer) = ExtractLawyers(text);

        var parties = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            plaintiff,
            defendant,
            plaintiff_lawyer = plaintiffLawyer,
            defendant_lawyer = defendantLawyer
        }));

        var (risk, riskJustification) = AssessRisk(text);
        var deadlines = ExtractDeadlines(text);
        var deadlinesDoc = JsonDocument.Parse(JsonSerializer.Serialize(deadlines));

        return new AnalysisResult
        {
            Parties = parties,
            CaseValue = ExtractCaseValue(text),
            CurrentPhase = ExtractPhase(text),
            LastDecision = ExtractLastDecision(text),
            NextDeadlines = deadlinesDoc,
            RiskLevel = risk,
            RiskJustification = riskJustification
        };
    }

    private static (string? plaintiff, string? defendant) ExtractParties(string text)
    {
        string? plaintiff = null;
        string? defendant = null;

        foreach (var marker in PlaintiffMarkers)
        {
            var name = ExtractNameAfterMarker(text, marker);
            if (name != null) { plaintiff = name; break; }
        }

        foreach (var marker in DefendantMarkers)
        {
            var name = ExtractNameAfterMarker(text, marker);
            if (name != null) { defendant = name; break; }
        }

        // Fallback: padrão "NOME vs NOME" ou "NOME X NOME"
        if (plaintiff == null && defendant == null)
        {
            var vsMatch = Regex.Match(text,
                @"([A-ZÁÉÍÓÚÂÊÎÔÛÃÕÇÀÜ][A-ZÁÉÍÓÚÂÊÎÔÛÃÕÇÀÜa-záéíóúâêîôûãõçàü\s]{3,40})\s+[xX]{1,3}\s+([A-ZÁÉÍÓÚÂÊÎÔÛÃÕÇÀÜ][A-ZÁÉÍÓÚÂÊÎÔÛÃÕÇÀÜa-záéíóúâêîôûãõçàü\s]{3,40})");
            if (vsMatch.Success)
            {
                plaintiff = vsMatch.Groups[1].Value.Trim();
                defendant = vsMatch.Groups[2].Value.Trim();
            }
        }

        return (plaintiff, defendant);
    }

    private static (string? plaintiffLawyer, string? defendantLawyer) ExtractLawyers(string text)
    {
        string? first = null;
        string? second = null;

        var oabMatches = Regex.Matches(text, @"(?:Dr\.?|Dra\.?|Advogad[oa])[:\s]+([A-ZÁÉÍÓÚ][a-záéíóúA-ZÁÉÍÓÚ\s\.]{5,50})");
        if (oabMatches.Count > 0) first = oabMatches[0].Groups[1].Value.Trim().TrimEnd(',', ';', '.');
        if (oabMatches.Count > 1) second = oabMatches[1].Groups[1].Value.Trim().TrimEnd(',', ';', '.');

        return (first, second);
    }

    private static string? ExtractNameAfterMarker(string text, string marker)
    {
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var after = text[(idx + marker.Length)..].TrimStart(' ', '\t');
        var match = Regex.Match(after, @"^([A-ZÁÉÍÓÚÂÊÎÔÛÃÕÇÀÜ][^\n\r,;]{3,80})");
        if (!match.Success) return null;

        return match.Groups[1].Value.Trim().TrimEnd(',', ';', '.', ' ');
    }

    private static decimal? ExtractCaseValue(string text)
    {
        var patterns = new[]
        {
            @"[Vv]alor\s+da\s+[Cc]ausa[:\s]+R\$\s*([\d.,]+)",
            @"[Vv]alor\s+[Aa]tribuído[:\s]+R\$\s*([\d.,]+)",
            @"R\$\s*([\d]{1,3}(?:[.,]\d{3})*(?:[.,]\d{2})?)",
        };

        foreach (var pattern in patterns)
        {
            var m = Regex.Match(text, pattern);
            if (!m.Success) continue;

            var raw = m.Groups[1].Value
                .Replace(".", "")
                .Replace(",", ".");

            if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var val))
                return val;
        }

        return null;
    }

    private static string ExtractPhase(string text)
    {
        var lower = text.ToLowerInvariant();

        // Procura pela fase mais avançada mencionada (ordem de precedência)
        var priority = new[]
        {
            "cumprimento de sentença", "execução", "embargos", "acórdão",
            "apelação", "recurso", "liquidação", "sentença", "perícia",
            "réplica", "contestação", "audiência de instrução",
            "audiência de conciliação", "citação", "despacho"
        };

        foreach (var keyword in priority)
        {
            if (lower.Contains(keyword) && PhaseKeywords.TryGetValue(keyword, out var phase))
                return phase;
        }

        return "Fase não identificada — verificar manualmente";
    }

    private static string ExtractLastDecision(string text)
    {
        var decisionMarkers = new[]
        {
            "DECIDO", "JULGO", "CONDENO", "ABSOLVO", "DEFIRO", "INDEFIRO",
            "DETERMINO", "HOMOLOGO", "DECLARO", "EXTINGO", "Decide-se", "decide-se",
            "Ante o exposto", "ante o exposto", "Pelo exposto", "pelo exposto",
            "Diante do exposto", "diante do exposto"
        };

        foreach (var marker in decisionMarkers)
        {
            var idx = text.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            // Extrai o parágrafo onde está o marcador
            var start = Math.Max(0, idx);
            var snippet = text[start..Math.Min(text.Length, start + 600)];
            var sentences = snippet.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);

            if (sentences.Length > 0)
            {
                var decision = string.Join(". ", sentences.Take(3)).Trim();
                if (decision.Length > 20)
                    return decision.Length > 400 ? decision[..400] + "..." : decision;
            }
        }

        // Fallback: últimas linhas do documento
        var lastLines = text.Split('\n').Where(l => l.Trim().Length > 20).TakeLast(5);
        var fallback = string.Join(" ", lastLines).Trim();
        return fallback.Length > 20
            ? (fallback.Length > 300 ? fallback[..300] + "..." : fallback)
            : "Decisão não identificada no documento";
    }

    private static List<string> ExtractDeadlines(string text)
    {
        var deadlines = new List<string>();

        // Padrão: "prazo de X dias" ou "até DD/MM/YYYY"
        var prazoMatches = Regex.Matches(text,
            @"[Pp]razo\s+de\s+(\d+)\s+(?:dias?|horas?)(?:\s+para\s+([^.]{5,60}))?");
        foreach (Match m in prazoMatches.Take(3))
        {
            var days = m.Groups[1].Value;
            var purpose = m.Groups[2].Success ? $" para {m.Groups[2].Value.Trim()}" : "";
            deadlines.Add($"Prazo de {days} dias{purpose}");
        }

        // Datas explícitas próximas de "prazo", "audiência", "até"
        var dateMatches = Regex.Matches(text,
            @"(?:[Aa]té|[Aa]udiência|[Pp]razo|[Jj]ulgamento)\s[^.]{0,30}(\d{2}/\d{2}/\d{4})");
        foreach (Match m in dateMatches.Take(3))
        {
            var date = m.Groups[1].Value;
            if (!deadlines.Any(d => d.Contains(date)))
                deadlines.Add($"Data prevista: {date}");
        }

        return deadlines.Count > 0 ? deadlines : ["Sem prazos identificados no documento"];
    }

    private static (RiskLevel level, string justification) AssessRisk(string text)
    {
        var lower = text.ToLowerInvariant();

        // Verifica na ordem: mais específico para menos específico
        var checks = new[]
        {
            ("parcialmente procedente", RiskLevel.Medium, "Pedido julgado parcialmente procedente — risco moderado"),
            ("improcedente", RiskLevel.High, "Pedido julgado improcedente — resultado desfavorável ao autor"),
            ("procedente", RiskLevel.Low, "Pedido julgado procedente — resultado favorável ao autor"),
            ("desprovido", RiskLevel.High, "Recurso desprovido — decisão mantida desfavoravelmente"),
            ("não provido", RiskLevel.High, "Recurso não provido"),
            ("provido", RiskLevel.Low, "Recurso provido — decisão favorável"),
            ("condenou", RiskLevel.High, "Parte condenada — risco alto de execução"),
            ("absolveu", RiskLevel.Low, "Parte absolvida — resultado favorável"),
            ("extinto sem resolução", RiskLevel.Medium, "Processo extinto sem julgamento do mérito"),
            ("extinto com resolução", RiskLevel.Low, "Processo extinto com resolução do mérito"),
        };

        foreach (var (keyword, level, justification) in checks)
        {
            if (lower.Contains(keyword))
                return (level, justification);
        }

        return (RiskLevel.Medium, "Resultado não identificado explicitamente — análise manual recomendada");
    }
}
