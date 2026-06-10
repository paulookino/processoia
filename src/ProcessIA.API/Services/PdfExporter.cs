using System.Text.Json;
using ProcessIA.API.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProcessIA.API.Services;

public static class PdfExporter
{
    public static byte[] Generate(Report report)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var parties = JsonSerializer.Deserialize<Dictionary<string, string?>>(
            report.Parties.RootElement.GetRawText()) ?? [];

        var deadlines = JsonSerializer.Deserialize<List<string>>(
            report.NextDeadlines.RootElement.GetRawText()) ?? [];

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text("ProcessIA").FontSize(22).Bold().FontColor("#4f6ef7");
                    col.Item().Text("Relatório de Análise de Processo Jurídico")
                        .FontSize(13).FontColor("#555555");
                    col.Item().PaddingTop(4).Text($"Gerado em: {report.CreatedAt:dd/MM/yyyy HH:mm}")
                        .FontSize(9).FontColor("#888888");
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor("#dddddd");
                });

                page.Content().PaddingTop(20).Column(col =>
                {
                    Section(col, "Partes Envolvidas", () =>
                    {
                        Field(col, "Autor", parties.GetValueOrDefault("plaintiff"));
                        Field(col, "Réu", parties.GetValueOrDefault("defendant"));
                        Field(col, "Advogado do Autor", parties.GetValueOrDefault("plaintiff_lawyer"));
                        Field(col, "Advogado do Réu", parties.GetValueOrDefault("defendant_lawyer"));
                    });

                    Section(col, "Informações do Processo", () =>
                    {
                        Field(col, "Valor da Causa", report.CaseValue.HasValue
                            ? $"R$ {report.CaseValue.Value:N2}" : "Não identificado");
                        Field(col, "Fase Atual", report.CurrentPhase);
                    });

                    Section(col, "Última Decisão", () =>
                    {
                        col.Item().PaddingTop(4).Text(report.LastDecision).FontColor("#333333");
                    });

                    if (deadlines.Any())
                    {
                        Section(col, "Próximos Prazos", () =>
                        {
                            foreach (var deadline in deadlines)
                                col.Item().Text($"• {deadline}");
                        });
                    }

                    Section(col, "Nível de Risco", () =>
                    {
                        var (color, label) = report.RiskLevel switch
                        {
                            RiskLevel.High => ("#cc0000", "ALTO"),
                            RiskLevel.Medium => ("#cc7700", "MÉDIO"),
                            _ => ("#007700", "BAIXO")
                        };

                        col.Item().PaddingTop(4).Text(label).Bold().FontColor(color).FontSize(14);
                        col.Item().PaddingTop(4).Text(report.RiskJustification).FontColor("#444444");
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("ProcessIA — ").FontColor("#aaaaaa").FontSize(9);
                    t.Span("Análise automatizada de processos jurídicos").FontColor("#aaaaaa").FontSize(9);
                });
            });
        }).GeneratePdf();
    }

    private static void Section(ColumnDescriptor col, string title, Action content)
    {
        col.Item().PaddingTop(16).Text(title).Bold().FontSize(12).FontColor("#4f6ef7");
        col.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor("#e0e0e0");
        content();
    }

    private static void Field(ColumnDescriptor col, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        col.Item().PaddingTop(6).Row(row =>
        {
            row.ConstantItem(160).Text(label + ":").SemiBold().FontColor("#555555");
            row.RelativeItem().Text(value).FontColor("#222222");
        });
    }
}
