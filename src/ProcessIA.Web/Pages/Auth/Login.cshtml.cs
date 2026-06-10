using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProcessIA.Web.Services;

namespace ProcessIA.Web.Pages.Auth;

public class LoginModel(ApiClient api) : PageModel
{
    public string? Error { get; set; }
    public string? Success { get; set; }

    public void OnGet([FromQuery] string? registered)
    {
        if (registered == "true") Success = "Conta criada com sucesso. Faça login.";
    }

    public async Task<IActionResult> OnPostAsync(string email, string password)
    {
        var (ok, error) = await api.LoginAsync(email, password);
        if (!ok) { Error = error; return Page(); }
        return RedirectToPage("/Dashboard/Index");
    }
}
