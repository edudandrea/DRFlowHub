using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class Unidade
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int? EmpresaId { get; set; }
        public Empresa? EmpresaCadastro { get; set; }
        public int NumeroRevenda { get; set; }
        public string Empresa { get; set; } = string.Empty;
        public string Revenda { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;
        public DateTime DataCadastro { get; set; }
        public ICollection<Users> Usuarios { get; set; } = new List<Users>();
    }
}
