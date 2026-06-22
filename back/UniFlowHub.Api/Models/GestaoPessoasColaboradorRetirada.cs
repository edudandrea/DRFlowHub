using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class GestaoPessoasColaboradorRetirada
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int ColaboradorId { get; set; }
        public GestaoPessoasColaborador? Colaborador { get; set; }
        public int ItemId { get; set; }
        public GestaoPessoasItem? Item { get; set; }
        public int Quantidade { get; set; } = 1;
        public DateTime DataRetirada { get; set; }
        public DateTime? DataDevolucao { get; set; }
        public string Status { get; set; } = "Retirado";
        public string Observacoes { get; set; } = string.Empty;
    }
}
