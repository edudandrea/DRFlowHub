using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class GestaoPessoasEtapa
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string TipoProcesso { get; set; } = string.Empty;
        public int Ordem { get; set; }
        public bool Ativa { get; set; } = true;
        public DateTime DataCadastro { get; set; }
        public DateTime? DataAtualizacao { get; set; }
    }
}
