namespace Cylo_Backend.Models.DTOs
{
    public class LoginDto
    {
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }
    }

    public class ResetPasswordDto
    {
        public required string Token { get; set; }
        public required string NewPassword { get; set; }
    }

    public class ForgotPasswordDto
    {
        public required string Email { get; set; }
    }
}