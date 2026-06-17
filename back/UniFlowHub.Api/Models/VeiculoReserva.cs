using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class VeiculoReserva
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Chassi { get; set; } = string.Empty;
        public bool Reservado { get; set; }
        public DateTime DataAtualizacao { get; set; }
        public int AtualizadoPorUserId { get; set; }
        public Users? AtualizadoPorUser { get; set; }
    }
}
