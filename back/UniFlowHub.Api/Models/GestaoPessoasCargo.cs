using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class GestaoPessoasCargo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public bool Ativo { get; set; } = true;
        public DateTime DataCadastro { get; set; }
        public DateTime? DataAtualizacao { get; set; }
        public ICollection<GestaoPessoasCargoItem> Itens { get; set; } = new List<GestaoPessoasCargoItem>();
        public ICollection<GestaoPessoasCargoAcesso> Acessos { get; set; } = new List<GestaoPessoasCargoAcesso>();
        public ICollection<GestaoPessoasColaborador> Colaboradores { get; set; } = new List<GestaoPessoasColaborador>();
    }
}
