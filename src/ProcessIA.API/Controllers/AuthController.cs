using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ProcessIA.API.Models;

namespace ProcessIA.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<User> userManager, IConfiguration config) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var user = new User { UserName = req.Email, Email = req.Email };
        var result = await userManager.CreateAsync(user, req.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        return Ok(new { message = "Conta criada. Faça login para continuar." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, req.Password))
            return Unauthorized(new { error = "E-mail ou senha inválidos." });

        var token = GenerateToken(user);

        return Ok(new
        {
            token,
            email = user.Email,
            subscriptionStatus = user.SubscriptionStatus.ToString()
        });
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email!)
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
