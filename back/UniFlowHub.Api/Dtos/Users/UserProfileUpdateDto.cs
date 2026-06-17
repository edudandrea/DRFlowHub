namespace UniFlowHub.Api.Dtos
{
    public class UserProfileUpdateDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public DateTime DataNascimento { get; set; }
    }
}
