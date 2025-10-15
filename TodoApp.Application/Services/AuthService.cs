using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entitites;
using TodoApp.Domain.Interfaces;
namespace TodoApp.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public AuthService(IUserRepository userRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
        }

        public async Task<TokenDTO> RegisterAsync(RegisterDTO request)
        {
            // Email kontrolü
            var existingUser = await _userRepository.GetByEmailAsync(request.Email);
            if (existingUser != null)
                throw new Exception("Bu email ile kayıtlı kullanıcı zaten mevcut.");

            // Parola hashleme
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Yeni kullanıcı oluştur
            var user = new User
            {
                Username = request.Username, // ✅ Eklendi
                Email = request.Email,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user);

            // Gerçek token üret
            return await GenerateTokenAsync(user);
        }

        public async Task<TokenDTO> LoginAsync(LoginDTO request)
        {
            // Kullanıcıyı bul
            var user = await _userRepository.GetByEmailAsync(request.Email);
            if (user == null)
                throw new Exception("Email veya şifre hatalı.");

            // Şifre kontrolü
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!isPasswordValid)
                throw new Exception("Email veya şifre hatalı.");

            // Token üret
            return await GenerateTokenAsync(user);
        }

        public async Task<TokenDTO> RefreshTokenAsync(string refreshToken)
        {
            // Refresh token'a sahip kullanıcıyı bul
            var user = await _userRepository.GetByRefreshTokenAsync(refreshToken);

            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                throw new Exception("Geçersiz veya süresi dolmuş refresh token.");

            // Yeni token üret
            return await GenerateTokenAsync(user);
        }

        // ✅ Yardımcı metod: Gerçek JWT token üretimi
        private async Task<TokenDTO> GenerateTokenAsync(User user)
        {
            // JWT claims
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // JWT secret key
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"] ?? throw new Exception("JWT Secret Key bulunamadı"))
            );
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Token expiration
            var expiration = DateTime.UtcNow.AddMinutes(
                int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60")
            );

            // JWT token oluştur
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: expiration,
                signingCredentials: creds
            );

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

            // Refresh token oluştur
            var refreshToken = GenerateRefreshToken();

            // Refresh token'ı veritabanına kaydet
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7); // 7 gün geçerli
            await _userRepository.UpdateAsync(user);

            return new TokenDTO
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Expiration = expiration
            };
        }

        // ✅ Refresh token üretimi
        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}