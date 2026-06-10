using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProcessIA.API.Data;
using ProcessIA.API.Models;

namespace ProcessIA.API.Controllers;

[Authorize]
[ApiController]
[Route("api/reports")]
public class ReportsController(AppDbContext db, UserManager<User> userManager) : ControllerBase
{
    [HttpGet("{processId:guid}")]
    public async Task<IActionResult> Get(Guid processId)
    {
        var userId = userManager.GetUserId(User)!;

        var report = await db.Reports
            .Include(r => r.Process)
            .Where(r => r.ProcessId == processId && r.Process.UserId == userId)
            .FirstOrDefaultAsync();

        if (report is null) return NotFound();

        return Ok(new
        {
            report.Id,
            report.ProcessId,
            fileName = report.Process.FileName,
            parties = report.Parties,
            report.CaseValue,
            report.CurrentPhase,
            report.LastDecision,
            nextDeadlines = report.NextDeadlines,
            riskLevel = report.RiskLevel.ToString(),
            report.RiskJustification,
            report.CreatedAt
        });
    }

    [HttpGet("{processId:guid}/pdf")]
    public async Task<IActionResult> ExportPdf(Guid processId)
    {
        var userId = userManager.GetUserId(User)!;

        var report = await db.Reports
            .Include(r => r.Process)
            .Where(r => r.ProcessId == processId && r.Process.UserId == userId)
            .FirstOrDefaultAsync();

        if (report is null) return NotFound();

        var pdfBytes = PdfExporter.Generate(report);
        var fileName = $"relatorio-{report.Process.FileName.Replace(".pdf", "")}-{DateTime.UtcNow:yyyyMMdd}.pdf";

        return File(pdfBytes, "application/pdf", fileName);
    }
}
