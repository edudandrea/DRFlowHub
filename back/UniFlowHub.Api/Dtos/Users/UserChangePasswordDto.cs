namespace UniFlowHub.Api.Dtos
{
    public class UserChangePasswordDto
    {
        public string SenhaAtual { get; set; } = string.Empty;
        public string NovaSenha { get; set; } = string.Empty;
    }
}
