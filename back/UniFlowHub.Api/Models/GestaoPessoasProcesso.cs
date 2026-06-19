using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class GestaoPessoasProcesso
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string TipoProcesso { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public string Unidade { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string ColaboradorNome { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Prioridade { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public DateTime DataSolicitacao { get; set; }
        public DateTime? DataAprovacaoGestor { get; set; }
        public string AprovadorGestor { get; set; } = string.Empty;
        public string ObservacoesAprovacao { get; set; } = string.Empty;
        public DateTime? DataCancelamento { get; set; }
        public string MotivoCancelamento { get; set; } = string.Empty;
        public DateTime? DataConclusao { get; set; }
        public int? EtapaAtualId { get; set; }
        public GestaoPessoasEtapa? EtapaAtual { get; set; }
        public int Userid { get; set; }
        public Users? OwnerUser { get; set; }
        public ICollection<GestaoPessoasProcessoHistorico> Historico { get; set; } = new List<GestaoPessoasProcessoHistorico>();
    }
}
