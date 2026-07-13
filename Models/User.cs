using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Cylo_Backend.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string? Name { get; set; }

        [Required]
        public string? Email { get; set; }

        public string? PasswordHash { get; set; }

        public string? ProfilePicture { get; set; }

        // These fields are now fully mapped and will be created as columns in the database
        public string? PaystackSubaccountCode { get; set; }

        public string? PaystackRecipientCode { get; set; }

        public string? TradeSafeRecipientId { get; set; }

        public string? BankName { get; set; }

        public string? AccountNumber { get; set; }

        public string? AccountName { get; set; }

        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        public string? Mobile { get; set; }

        public bool IsFicaVerified { get; set; } = false;

        public string? PasswordResetToken { get; set; }

        public DateTime? ResetTokenExpires { get; set; }

        // Navigation properties (EF handles these differently, leave them as they are)
        [JsonIgnore]
        public Profile? Profile { get; set; }

        public ICollection<Follow> Followers { get; set; } = new List<Follow>();
        public ICollection<Follow> Following { get; set; } = new List<Follow>();
        public ICollection<Post> Posts { get; set; } = new List<Post>();
        public ICollection<Like> Likes { get; set; } = new List<Like>();
    }
}