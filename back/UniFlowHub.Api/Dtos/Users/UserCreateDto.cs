namespace UniFlowHub.Api.Dtos
{
    public class UserCreateDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Senha  { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public List<string> Perfis { get; set; } = new();
        public string Departamento { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public bool Ativo { get; set; } = true;
        public int? UnidadeId { get; set; }
        public DateTime DataNascimento { get; set; }
    }
}
