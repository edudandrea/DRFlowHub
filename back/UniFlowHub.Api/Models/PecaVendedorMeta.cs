using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class PecaVendedorMeta
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string CpfVendedor { get; set; } = string.Empty;
        public string NomeVendedor { get; set; } = string.Empty;
        public decimal ValorMeta { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public DateTime DataAtualizacao { get; set; }
        public int AtualizadoPorUserId { get; set; }
        public Users? AtualizadoPorUser { get; set; }
    }
}
