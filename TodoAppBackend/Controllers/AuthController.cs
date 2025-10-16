using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Services;
using TodoApp.Domain.Interfaces;
using System.IdentityModel.Tokens.Jwt;

namespace TodoAppBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUserRepository _userRepository;
        private readonly TwoFactorAuthService _twoFactorService;

        public AuthController(
            IAuthService authService,
            IUserRepository userRepository,
            TwoFactorAuthService twoFactorService)
        {
            _authService = authService;
            _userRepository = userRepository;
            _twoFactorService = twoFactorService;
        }

        // POST: api/auth/register
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO request)
        {
            try
            {
                var result = await _authService.RegisterAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST: api/auth/refresh
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] string refreshToken)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(refreshToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET: api/auth/me
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userRepository.GetByIdAsync(int.Parse(userId!));

            // Token'dan expiration bilgisini alınıyor
            var expClaim = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
            DateTime? tokenExpiration = null;
            TimeSpan? timeRemaining = null;

            if (expClaim != null)
            {
                // Unix timestamp'i DateTime'a çevir
                var expUnix = long.Parse(expClaim);
                tokenExpiration = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;

                // Kalan süreyi hesapla
                timeRemaining = tokenExpiration.Value - DateTime.UtcNow;
            }

            return Ok(new
            {
                id = user!.Id,
                username = user.Username,
                email = user.Email,
                twoFactorEnabled = user.TwoFactorEnabled,
                token = new
                {
                    expiresAt = tokenExpiration,
                    expiresIn = timeRemaining?.TotalMinutes > 0
                        ? $"{Math.Floor(timeRemaining.Value.TotalMinutes)} dakika {timeRemaining.Value.Seconds} saniye"
                        : "Süresi dolmuş",
                    remainingMinutes = Math.Floor(timeRemaining?.TotalMinutes ?? 0),
                    remainingSeconds = timeRemaining?.Seconds ?? 0
                },
                message = "Token geçerli! Yetkilendirme başarılı ✅"
            });
        }
        // ✅ YENİ: POST: api/auth/enable-2fa
        [HttpPost("enable-2fa")]
        [Authorize]
        public async Task<IActionResult> Enable2FA()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var user = await _userRepository.GetByIdAsync(int.Parse(userId!));

                if (user == null)
                    return NotFound(new { message = "Kullanıcı bulunamadı" });

                if (user.TwoFactorEnabled)
                    return BadRequest(new { message = "2FA zaten aktif" });

                // Secret key oluştur
                var secretKey = _twoFactorService.GenerateSecretKey();

                // QR kod oluştur
                var qrCodeUrl = _twoFactorService.GenerateQRCodeUrl(user.Email, secretKey);
                var qrCodeImage = _twoFactorService.GenerateQRCodeImage(qrCodeUrl);

                // Secret key'i kaydet (henüz aktif etme)
                user.TwoFactorSecretKey = secretKey;
                await _userRepository.UpdateAsync(user);

                return Ok(new TwoFactorSetupDTO
                {
                    QRCodeImage = $"data:image/png;base64,{Convert.ToBase64String(qrCodeImage)}",
                    ManualEntryKey = secretKey,
                    Message = "QR kodu Google Authenticator ile tarayın ve 6 haneli kodu verify-2fa endpoint'ine gönderin"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ✅ YENİ: POST: api/auth/verify-2fa
        [HttpPost("verify-2fa")]
        [Authorize]
        public async Task<IActionResult> Verify2FA([FromBody] string code)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var user = await _userRepository.GetByIdAsync(int.Parse(userId!));

                if (user == null || string.IsNullOrEmpty(user.TwoFactorSecretKey))
                    return BadRequest(new { message = "2FA kurulumu yapılmamış. Önce enable-2fa çağırın" });

                if (string.IsNullOrEmpty(code))
                    return BadRequest(new { message = "2FA kodu gerekli" });

                // Kodu doğrula
                var isValid = _twoFactorService.ValidateCode(user.TwoFactorSecretKey, code);

                if (!isValid)
                    return BadRequest(new { message = "Geçersiz kod. Lütfen Google Authenticator'daki güncel kodu girin" });

                // 2FA'yı aktif et
                user.TwoFactorEnabled = true;
                await _userRepository.UpdateAsync(user);

                return Ok(new
                {
                    message = "2FA başarıyla aktif edildi! Bundan sonra login yaparken twoFactorCode alanını doldurmanız gerekecek",
                    twoFactorEnabled = true
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ✅ YENİ: POST: api/auth/disable-2fa
        [HttpPost("disable-2fa")]
        [Authorize]
        public async Task<IActionResult> Disable2FA([FromBody] string code)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var user = await _userRepository.GetByIdAsync(int.Parse(userId!));

                if (user == null)
                    return NotFound(new { message = "Kullanıcı bulunamadı" });

                if (!user.TwoFactorEnabled)
                    return BadRequest(new { message = "2FA zaten aktif değil" });

                if (string.IsNullOrEmpty(code))
                    return BadRequest(new { message = "Doğrulama için 2FA kodu gerekli" });

                // Kodu doğrula
                var isValid = _twoFactorService.ValidateCode(user.TwoFactorSecretKey!, code);

                if (!isValid)
                    return BadRequest(new { message = "Geçersiz kod" });

                // 2FA'yı devre dışı bırak
                user.TwoFactorEnabled = false;
                user.TwoFactorSecretKey = null;
                await _userRepository.UpdateAsync(user);

                return Ok(new
                {
                    message = "2FA devre dışı bırakıldı",
                    twoFactorEnabled = false
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}