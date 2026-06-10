using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProcessIA.Web.Services;

namespace ProcessIA.Web.Pages.Auth;

public class RegisterModel(ApiClient api) : PageModel
{
    public string? Error { get; set; }

    public async Task<IActionResult> OnPostAsync(string email, string password)
    {
        var (ok, error) = await api.RegisterAsync(email, password);
        if (!ok) { Error = error; return Page(); }
        return RedirectToPage("/Auth/Login", new { registered = "true" });
    }
}
