using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class GuiaIcmsPagamento
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string GuiaId { get; set; } = string.Empty;
        public string Status { get; set; } = "Pago";
        public DateTime DataPagamento { get; set; }
        public DateTime DataAtualizacao { get; set; }
        public int AtualizadoPorUserId { get; set; }
        public Users? AtualizadoPorUser { get; set; }
    }
}
