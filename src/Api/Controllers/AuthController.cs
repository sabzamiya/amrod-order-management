using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Amrod.OrderManagement.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;

namespace Amrod.OrderManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("token")]
    public ActionResult<TokenResponse> CreateToken(TokenRequest request)
    {
        var allowedPermissions = new[]
        {
            "Orders.Read",
            "Orders.Write",
            "Orders.Admin"
        };

        if (!allowedPermissions.Contains(request.Permission))
        {
            return BadRequest(new
            {
                message = "Invalid permission."
            });
        }

        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT signing key is not configured.");

        var issuer = _configuration["Jwt:Issuer"]
            ?? "Amrod.OrderManagement";

        var audience = _configuration["Jwt:Audience"]
            ?? "Amrod.OrderManagement.Client";

        var expiresAt = DateTime.UtcNow.AddHours(1);

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                "assessment-user"),

            new Claim(
                "permission",
                request.Permission)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return Ok(new TokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt));
    }
}