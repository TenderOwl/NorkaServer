using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Norka.API.Data;
using Norka.API.Entities;
using Norka.API.Models;
using Norka.API.Models.Request;
using Norka.API.Models.Response;
using Norka.API.Services;

namespace Norka.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController(NorkaDbContext db, UserManager<ApplicationUser> userManager, TokenService tokenService) : Controller
{
    [HttpPost("register")]
    public async Task<IActionResult> CreateUser(RegistrationRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await userManager.CreateAsync(
            new ApplicationUser
            {
                UserName = request.Email.Split('@')[0],
                Email = request.Email,
            },
            request.Password);

        if (result.Succeeded)
        {
            request.Password = "";
            return CreatedAtAction(nameof(CreateUser), new { email = request.Email }, request);
        }

        // Handle errors
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Code, error.Description);
        }

        return BadRequest(ModelState);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return BadRequest("Bad credentials");
        }

        var isPasswordValid = await userManager.CheckPasswordAsync(user, request.Password!);
        if (!isPasswordValid)
        {
            return BadRequest("Bad credentials");
        }

        var accessToken = tokenService.CreateToken(user);
        var refreshToken = tokenService.CreateRefreshToken(user);
        await db.SaveChangesAsync();

        return Ok(new AuthResponse
        {
            Email = user.Email!,
            Token = accessToken,
            RefreshToken = refreshToken,
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        return Ok(new UserResponse
        {
            Id = user.Id,
            Email = user.Email!,
            UserName = user.UserName ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken(RefreshTokenRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        
        var principal = tokenService.GetPrincipalFromExpiredToken(request.RefreshToken);
        if (principal == null)
        {
            return BadRequest("Invalid access token or refresh token");
        }
        
        var userId = principal.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value;
        var user = await userManager.FindByIdAsync(userId);

        if (user == null) return BadRequest("Invalid access token or refresh token");

        var accessToken = tokenService.CreateToken(user);
        var refreshToken = tokenService.CreateRefreshToken(user);
        await db.SaveChangesAsync();

        return Ok(new RefreshTokenResponse { Token = accessToken, });
    }
}