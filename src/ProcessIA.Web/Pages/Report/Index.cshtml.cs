using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProcessIA.Web.Services;

namespace ProcessIA.Web.Pages.Report;

public class ReportPageModel(ApiClient api) : PageModel
{
    public ReportDetail? Report { get; set; }
    public bool IsProcessing { get; set; }
    public bool Failed { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!api.IsAuthenticated) return RedirectToPage("/Auth/Login");

        var status = await api.GetStatusAsync(id);
        if (status is null) return NotFound();

        if (status.Status is "Done")
            Report = await api.GetReportAsync(id);
        else if (status.Status is "Pending" or "Processing")
            IsProcessing = true;
        else
            Failed = true;

        return Page();
    }
}
