namespace ProcessIA.API.Models;

public class LegalProcess
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = null!;
    public User User { get; set; } = null!;

    public string FileName { get; set; } = null!;
    public string BlobUrl { get; set; } = null!;
    public long FileSizeBytes { get; set; }

    public ProcessStatus Status { get; set; } = ProcessStatus.Pending;
    public string? ErrorMessage { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    public Report? Report { get; set; }
}

public enum ProcessStatus
{
    Pending,
    Processing,
    Done,
    Failed
}
