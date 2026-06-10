using Microsoft.EntityFrameworkCore;
using ProcessIA.API.Data;
using ProcessIA.API.Models;

namespace ProcessIA.API.Services;

public class ProcessingPipeline(
    AppDbContext db,
    BlobStorageService blob,
    OcrService ocr,
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
            logger.LogInformation("Starting pipeline for process {Id}", processId);

            var blobName = ExtractBlobName(process.BlobUrl);
            var signedUrl = blob.GetSignedUrl(blobName, TimeSpan.FromMinutes(10));

            var extractedText = await ocr.ExtractTextAsync(signedUrl);
            logger.LogInformation("OCR complete for {Id}: {Chars} chars extracted", processId, extractedText.Length);

            var analysis = await ai.AnalyzeAsync(extractedText);
            logger.LogInformation("AI analysis complete for {Id}, risk: {Risk}", processId, analysis.RiskLevel);

            var report = new Report
            {
                ProcessId = processId,
                Parties = analysis.Parties,
                CaseValue = analysis.CaseValue,
                CurrentPhase = analysis.CurrentPhase,
                LastDecision = analysis.LastDecision,
                NextDeadlines = analysis.NextDeadlines,
                RiskLevel = analysis.RiskLevel,
                RiskJustification = analysis.RiskJustification,
                RawExtractedText = extractedText
            };

            process.Status = ProcessStatus.Done;
            process.ProcessedAt = DateTime.UtcNow;

            db.Reports.Add(report);
            await db.SaveChangesAsync();

            logger.LogInformation("Pipeline complete for process {Id}", processId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pipeline failed for process {Id}", processId);

            process.Status = ProcessStatus.Failed;
            process.ErrorMessage = ex.Message;
            await db.SaveChangesAsync();
        }
    }

    private static string ExtractBlobName(string blobUrl)
    {
        var uri = new Uri(blobUrl);
        return uri.AbsolutePath.TrimStart('/').Split('/', 2)[1];
    }
}
