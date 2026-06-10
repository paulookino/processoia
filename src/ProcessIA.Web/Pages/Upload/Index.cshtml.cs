using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProcessIA.Web.Services;

namespace ProcessIA.Web.Pages.Upload;

public class UploadModel(ApiClient api) : PageModel
{
    public string? Error { get; set; }

    public IActionResult OnGet()
    {
        if (!api.IsAuthenticated) return RedirectToPage("/Auth/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(IFormFile file)
    {
        if (!api.IsAuthenticated) return RedirectToPage("/Auth/Login");

        var (ok, processId, error) = await api.UploadAsync(file);
        if (!ok) { Error = error; return Page(); }

        return RedirectToPage("/Report/Index", new { id = processId });
    }
}
