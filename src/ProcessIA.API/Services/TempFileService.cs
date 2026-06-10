namespace ProcessIA.API.Services;

public class TempFileService
{
    private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "processoia");

    public TempFileService()
    {
        Directory.CreateDirectory(TempDir);
    }

    public async Task<string> SaveAsync(Stream stream, string fileName)
    {
        var fileId = Guid.NewGuid().ToString();
        var dir = Path.Combine(TempDir, fileId);
        Directory.CreateDirectory(dir);

        var filePath = Path.Combine(dir, fileName);
        await using var fs = File.Create(filePath);
        await stream.CopyToAsync(fs);

        return filePath;
    }

    public void Delete(string filePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (dir != null && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch { /* best-effort cleanup */ }
    }
}
