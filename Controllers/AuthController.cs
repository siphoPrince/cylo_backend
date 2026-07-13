using Cylo_Backend.Data;
using Cylo_Backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Apis.Auth;
using Cylo_Backend.Models.DTOs;
using LoginDto = Cylo_Backend.Models.DTOs.LoginDto;

namespace Cylo_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;

        public AuthController(ApplicationDbContext context, IConfiguration configuration, EmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] string googleToken)
        {
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings()
                {
                    Audience = new List<string> { _configuration["Google:ClientId"] }
                };

                var payload = await GoogleJsonWebSignature.ValidateAsync(googleToken, settings);
                var emailNormalized = payload.Email.ToLower().Trim();
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == emailNormalized);

                if (user == null)
                {
                    user = new User
                    {
                        Name = payload.Name,
                        Email = emailNormalized,
                        PasswordHash = "GOOGLE_AUTH_USER",
                        ProfilePicture = payload.Picture,
                        IsFicaVerified = false
                    };

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                var token = CreateToken(user);
                return Ok(new { token = token, userId = user.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Invalid Google Token", error = ex.Message });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegisterDto request)
        {
            var emailNormalized = request.Email.ToLower().Trim();

            // 1. Double check if email is already taken
            if (await _context.Users.AnyAsync(u => u.Email == emailNormalized))
                return BadRequest("Email already in use.");

            // 2. Hash the raw password safely
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // 3. Build a tight, essential user instance
            var user = new User
            {
                Name = request.Name.Trim(),
                Email = emailNormalized,
                PasswordHash = hashedPassword
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User Registered Successfully" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto request)
        {
            var emailNormalized = request.Email.ToLower().Trim();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == emailNormalized);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.PasswordHash, user.PasswordHash))
                return Unauthorized("Invalid credentials.");

            var token = CreateToken(user);
            return Ok(new { token = token, userId = user.Id });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
        {
            var emailNormalized = request.Email.ToLower().Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == emailNormalized);

            if (user == null)
                return Ok(new { message = "If an account exists, a reset link has been sent." });

            user.PasswordResetToken = Convert.ToHexString(Guid.NewGuid().ToByteArray());
            user.ResetTokenExpires = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            // PRODUCTION FIX: Changed from localhost to your live website domain url
            var resetLink = $"https://cylosocials.co.za{user.PasswordResetToken}";

            string emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; border: 1px solid #eee; padding: 20px;'>
                    <div style='text-align: center; background-color: #003366; padding: 10px;'>
                        <h1 style='color: white; margin: 0;'>Cylo Security</h1>
                    </div>
                    <div style='padding: 20px;'>
                        <h2 style='color: #333;'>Password Reset Request</h2>
                        <p>Hi {user.Name},</p>
                        <p>We received a request to reset your password. Click the button below to set a new one. This link expires in 1 hour.</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{resetLink}' style='background-color: #003366; color: white; padding: 15px 25px; text-decoration: none; border-radius: 5px; font-weight: bold;'>Reset Password</a>
                        </div>
                        <p style='color: #777; font-size: 12px;'>If you did not request this, please ignore this email or contact support if you have concerns.</p>
                    </div>
                    <div style='border-top: 1px solid #eee; padding-top: 10px; font-size: 11px; color: #aaa; text-align: center;'>
                        Cylo Marketplace &copy; 2026 | Sandton, Johannesburg
                    </div>
                </div>";

            await _emailService.SendSecurityEmailAsync(user.Email, "Reset Your Cylo Password", emailBody);

            return Ok(new { message = "If an account exists, a reset link has been sent." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == request.Token);

            if (user == null || user.ResetTokenExpires < DateTime.UtcNow)
                return BadRequest("Invalid or expired token.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.PasswordResetToken = null;
            user.ResetTokenExpires = null;

            await _context.SaveChangesAsync();

            return Ok("Password successfully reset.");
        }

        private string CreateToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? "")
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
