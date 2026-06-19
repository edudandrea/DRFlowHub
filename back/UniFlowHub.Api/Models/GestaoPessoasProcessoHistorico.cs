using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class GestaoPessoasProcessoHistorico
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int ProcessoId { get; set; }
        public GestaoPessoasProcesso? Processo { get; set; }
        public int EtapaId { get; set; }
        public GestaoPessoasEtapa? Etapa { get; set; }
        public string Acao { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public int UsuarioId { get; set; }
        public string UsuarioNome { get; set; } = string.Empty;
        public DateTime DataMovimentacao { get; set; }
    }
}
