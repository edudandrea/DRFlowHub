using UniFlowHub.Api.Dtos;

namespace UniFlowHub.Api.Dtos.Auth
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserResponseDto User { get; set; } = new();
    }
}
