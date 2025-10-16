using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TodoApp.Application.DTOs
{
    public class TwoFactorSetupDTO
    {
        public string QRCodeImage { get; set; } = string.Empty; // Base64 image
        public string ManualEntryKey { get; set; } = string.Empty; // Manuel giriş için
        public string Message { get; set; } = string.Empty;
    }
}
