namespace DRFlowHub.Api.Dtos
{
    public class UserResponseDto
    {
        public int Id { get; set; }                
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public string RustDeskId { get; set; } = string.Empty;
        public string RustDeskHostname { get; set; } = string.Empty;
        public string RustDeskSistemaOperacional { get; set; } = string.Empty;
        public bool Ativo { get; set; } = true;
        public int? UnidadeId { get; set; }
        public string UnidadeNome { get; set; } = string.Empty;
        public DateTime DataNascimento { get; set; }
        public List<string> Acessos { get; set; } = new();
    }
}
