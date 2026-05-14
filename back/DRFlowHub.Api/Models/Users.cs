using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRFlowHub.Api.Models
{
    public class Users
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public string RustDeskId { get; set; } = string.Empty;
        public string RustDeskSenha { get; set; } = string.Empty;
        public string RustDeskHostname { get; set; } = string.Empty;
        public string RustDeskSistemaOperacional { get; set; } = string.Empty;
        public bool Ativo { get; set; } = true;
        public int? UnidadeId { get; set; }
        public Unidade? Unidade { get; set; }
        public int? CreatedByUserId { get; set; }
        public Users? CreatedByUser { get; set; }
        public DateTime DataNascimento { get; set; }
        public ICollection<Users>? CreatedUsers { get; set; }

        
    }
}
