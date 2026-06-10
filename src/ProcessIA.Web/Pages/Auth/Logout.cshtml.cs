using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProcessIA.Web.Services;

namespace ProcessIA.Web.Pages.Auth;

public class LogoutModel(ApiClient api) : PageModel
{
    public IActionResult OnGet() { api.Logout(); return RedirectToPage("/Auth/Login"); }
}
