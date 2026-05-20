using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Course.Domain.Entities;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Course.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email,
            FullName = request.FullName,
            PreferredTheme = "dark",
            PreferredCulture = "en",
            IsBlocked = false
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return BadRequest(ModelState);
        }

        // Add default role of User
        await _userManager.AddToRoleAsync(user, "User");

        return Ok(new { message = "Registration successful." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByNameAsync(request.UsernameOrEmail)
                   ?? await _userManager.FindByEmailAsync(request.UsernameOrEmail);

        if (user == null)
        {
            return Unauthorized(new { error = "Invalid credentials." });
        }

        if (user.IsBlocked)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Your account is blocked." });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!result.Succeeded)
        {
            return Unauthorized(new { error = "Invalid credentials." });
        }

        var token = await GenerateJwtTokenAsync(user);
        return Ok(new { token = token });
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new { error = "Google ID token is required." });
        }

        try
        {
            // Validate the Google ID Token
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, new GoogleJsonWebSignature.ValidationSettings());

            if (payload == null)
            {
                return Unauthorized(new { error = "Invalid Google token." });
            }

            var email = payload.Email;
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new { error = "Email claim is missing from Google token." });
            }

            // Find or link account
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                // Create external user
                user = new ApplicationUser
                {
                    UserName = email.Split('@')[0] + "_" + Guid.NewGuid().ToString().Substring(0, 5),
                    Email = email,
                    FullName = payload.Name,
                    PreferredTheme = "dark",
                    PreferredCulture = "en",
                    IsBlocked = false
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    return BadRequest(new { error = string.Join(" ", createResult.Errors.Select(e => e.Description)) });
                }

                await _userManager.AddToRoleAsync(user, "User");
            }
            else
            {
                // Update FullName if not set locally
                if (string.IsNullOrWhiteSpace(user.FullName) && !string.IsNullOrWhiteSpace(payload.Name))
                {
                    user.FullName = payload.Name;
                    await _userManager.UpdateAsync(user);
                }
            }

            if (user.IsBlocked)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Your account is blocked." });
            }

            // Generate application token
            var token = await GenerateJwtTokenAsync(user);
            return Ok(new { token = token });
        }
        catch (InvalidJwtException)
        {
            return Unauthorized(new { error = "Invalid Google token signature." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<string> GenerateJwtTokenAsync(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var jwtSection = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var expiryMinutes = double.Parse(jwtSection["ExpiryInMinutes"] ?? "180");
        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FullName { get; set; }
}

public class LoginRequest
{
    public string UsernameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class GoogleLoginRequest
{
    public string IdToken { get; set; } = string.Empty;
}
