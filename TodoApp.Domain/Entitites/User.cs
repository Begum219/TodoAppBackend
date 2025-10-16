using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TodoApp.Domain.Entitites
{
    public class User
    {
        public int Id { get; set; }
        
        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }= DateTime.UtcNow;

        public string? RefreshToken { get; set; }

        public DateTime? RefreshTokenExpiryTime { get; set; }
        
        // 2 faktörlü doğrulama için eklendi 
        public bool TwoFactorEnabled { get; set; } = false;
        public string? TwoFactorSecretKey { get; set; }


    }
}
