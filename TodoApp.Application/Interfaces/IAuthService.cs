using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TodoApp.Application.DTOs;

namespace TodoApp.Application.Interfaces
{
    public interface IAuthService
    {
        Task<TokenDTO> RegisterAsync(RegisterDTO request );
        Task<TokenDTO> LoginAsync(LoginDTO  request);
        Task<TokenDTO> RefreshTokenAsync(string refreshToken);

    }
}
