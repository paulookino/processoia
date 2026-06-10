using Azure;
using Azure.AI.DocumentIntelligence;

namespace ProcessIA.API.Services;

public class OcrService(IConfiguration config)
{
    private readonly DocumentIntelligenceClient _client = new(
        new Uri(config["Azure:DocumentIntelligence:Endpoint"]!),
        new AzureKeyCredential(config["Azure:DocumentIntelligence:ApiKey"]!)
    );

    public async Task<string> ExtractTextAsync(Uri documentUri)
    {
        var operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-read",
            documentUri
        );

        var result = operation.Value;

        var text = string.Join("\n\n", result.Pages.Select(page =>
            string.Join("\n", page.Lines.Select(line => line.Content))
        ));

        return text;
    }
}
