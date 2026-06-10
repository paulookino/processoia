using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProcessIA.Web.Services;

namespace ProcessIA.Web.Pages.Dashboard;

public class IndexModel(ApiClient api) : PageModel
{
    public List<ProcessSummary> Processes { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!api.IsAuthenticated) return RedirectToPage("/Auth/Login");
        Processes = await api.GetProcessesAsync();
        return Page();
    }
}
