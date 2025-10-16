
using OtpNet;
using QRCoder;

namespace TodoApp.Application.Services
{
    public class TwoFactorAuthService
    {
        // Rastgele secret key oluşturur
        public string GenerateSecretKey()
        {
            var key = KeyGeneration.GenerateRandomKey(20);
            return Base32Encoding.ToString(key);
        }

        // QR kod için URL oluşturur
        public string GenerateQRCodeUrl(string email, string secretKey)
        {
            var issuer = "TodoApp";
            var otpauthUrl = $"otpauth://totp/{issuer}:{email}?secret={secretKey}&issuer={issuer}";
            return otpauthUrl;
        }

        // QR kod görüntüsü oluşturur (PNG)
        public byte[] GenerateQRCodeImage(string qrCodeUrl)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrCodeUrl, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(20);
        }

        // 6 haneli kodu doğrular
        public bool ValidateCode(string secretKey, string code)
        {
            var key = Base32Encoding.ToBytes(secretKey);
            var totp = new Totp(key);
            return totp.VerifyTotp(code, out _, new VerificationWindow(2, 2));
        }
    }
}