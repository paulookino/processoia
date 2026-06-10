using ProcessIA.API.Data;
using ProcessIA.API.Models;

namespace ProcessIA.API.Services;

public class ProcessingPipeline(
    AppDbContext db,
    TempFileService files,
    PdfTextExtractor extractor,
    AiAnalysisService ai,
    ILogger<ProcessingPipeline> logger)
{
    public async Task ExecuteAsync(Guid processId)
    {
        var process = await db.Processes.FindAsync(processId);
        if (process is null) return;

        process.Status = ProcessStatus.Processing;
        await db.SaveChangesAsync();

        try
        {
            logger.LogInformation("Starting pipeline for {Id}", processId);

            var text = await extractor.ExtractAsync(process.FilePath);
            logger.LogInformation("Text extraction done for {Id}: {Chars} chars", processId, text.Length);

            var analysis = await ai.AnalyzeAsync(text);
            logger.LogInformation("AI analysis done for {Id}, risk: {Risk}", processId, analysis.RiskLevel);

            db.Reports.Add(new Report
            {
                ProcessId = processId,
                Parties = analysis.Parties,
                CaseValue = analysis.CaseValue,
                CurrentPhase = analysis.CurrentPhase,
                LastDecision = analysis.LastDecision,
                NextDeadlines = analysis.NextDeadlines,
                RiskLevel = analysis.RiskLevel,
                RiskJustification = analysis.RiskJustification,
                RawExtractedText = text
            });

            process.Status = ProcessStatus.Done;
            process.ProcessedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            files.Delete(process.FilePath);
            logger.LogInformation("Pipeline complete for {Id}", processId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pipeline failed for {Id}", processId);
            process.Status = ProcessStatus.Failed;
            process.ErrorMessage = ex.Message;
            await db.SaveChangesAsync();
        }
    }
}
