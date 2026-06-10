using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProcessIA.Web.Services;

namespace ProcessIA.Web.Pages;

public class IndexModel(ApiClient api) : PageModel
{
    public IActionResult OnGet() =>
        api.IsAuthenticated ? RedirectToPage("/Dashboard/Index") : RedirectToPage("/Auth/Login");
}
