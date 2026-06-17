using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class SolicitacaoCompraComunicacao
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int SolicitacaoCompraId { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public string AutorNome { get; set; } = string.Empty;
        public string AutorRole { get; set; } = string.Empty;
        public int AutorUserId { get; set; }
        public DateTime DataCriacao { get; set; }
        public SolicitacaoCompra? SolicitacaoCompra { get; set; }
    }
}
