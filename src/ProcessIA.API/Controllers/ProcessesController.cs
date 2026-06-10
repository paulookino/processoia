using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProcessIA.API.Data;
using ProcessIA.API.Models;
using ProcessIA.API.Services;

namespace ProcessIA.API.Controllers;

[Authorize]
[ApiController]
[Route("api/processes")]
public class ProcessesController(
    AppDbContext db,
    TempFileService files,
    UserManager<User> userManager) : ControllerBase
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = "Arquivo muito grande. Limite: 50MB." });

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Apenas arquivos PDF são aceitos." });

        await using var stream = file.OpenReadStream();
        var filePath = await files.SaveAsync(stream, file.FileName);

        var process = new LegalProcess
        {
            UserId = user.Id,
            FileName = file.FileName,
            FilePath = filePath,
            FileSizeBytes = file.Length
        };

        db.Processes.Add(process);
        await db.SaveChangesAsync();

        BackgroundJob.Enqueue<ProcessingPipeline>(p => p.ExecuteAsync(process.Id));

        return Accepted(new { processId = process.Id, status = process.Status.ToString() });
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = userManager.GetUserId(User)!;

        var processes = await db.Processes
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.UploadedAt)
            .Select(p => new
            {
                p.Id,
                p.FileName,
                p.FileSizeBytes,
                Status = p.Status.ToString(),
                p.UploadedAt,
                p.ProcessedAt,
                hasReport = p.Report != null
            })
            .ToListAsync();

        return Ok(processes);
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id)
    {
        var userId = userManager.GetUserId(User)!;

        var process = await db.Processes
            .Where(p => p.Id == id && p.UserId == userId)
            .Select(p => new { p.Id, Status = p.Status.ToString(), p.ErrorMessage })
            .FirstOrDefaultAsync();

        if (process is null) return NotFound();

        return Ok(process);
    }
}
