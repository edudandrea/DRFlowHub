using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class GestaoPessoasColaborador
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public int? CargoId { get; set; }
        public GestaoPessoasCargo? Cargo { get; set; }
        public int? UnidadeId { get; set; }
        public Unidade? Unidade { get; set; }
        public DateTime? DataNascimento { get; set; }
        public DateTime? DataAdmissao { get; set; }
        public string Status { get; set; } = "Ativo";
        public string Observacoes { get; set; } = string.Empty;
        public DateTime DataCadastro { get; set; }
        public DateTime? DataAtualizacao { get; set; }
        public ICollection<GestaoPessoasColaboradorRetirada> Retiradas { get; set; } = new List<GestaoPessoasColaboradorRetirada>();
    }
}
