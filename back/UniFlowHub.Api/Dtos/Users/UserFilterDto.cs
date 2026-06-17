namespace UniFlowHub.Api.Dtos
{
    public class UserFilterDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Senha  { get; set; } = string.Empty;
        public DateTime DataNascimento { get; set; }
    }
}