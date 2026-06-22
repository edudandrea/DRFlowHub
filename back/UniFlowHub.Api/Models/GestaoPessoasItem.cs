using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class GestaoPessoasItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Tamanho { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public bool Ativo { get; set; } = true;
        public DateTime DataCadastro { get; set; }
        public DateTime? DataAtualizacao { get; set; }
        public ICollection<GestaoPessoasCargoItem> Cargos { get; set; } = new List<GestaoPessoasCargoItem>();
        public ICollection<GestaoPessoasColaboradorRetirada> Retiradas { get; set; } = new List<GestaoPessoasColaboradorRetirada>();
    }
}
