using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ProcessIA.API.Services;

public class PdfTextExtractor
{
    public Task<string> ExtractAsync(string filePath)
    {
        using var pdf = PdfDocument.Open(filePath);

        var pages = pdf.GetPages().Select(page =>
            string.Join(" ", page.GetWords().Select(w => w.Text)));

        var text = string.Join("\n\n", pages);

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException(
                "Não foi possível extrair texto deste PDF. " +
                "O arquivo pode ser uma imagem escaneada sem camada de texto.");

        return Task.FromResult(text);
    }
}
