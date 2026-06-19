using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UniFlowHub.Api.Dtos.SolicitacoesRH
{
    public class SolicitacoesRHResponseDto
    {
        public int Id { get; set; }
        public string Unidade { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string TipoSolicitacao { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string AnexossUrl { get; set; } = string.Empty;
        public string  Prioridade { get; set; } = string.Empty;
        public string Responsavel { get; set; } = string.Empty;
        public DateTime DataSolicitacao { get; set; }
        public DateTime? DataEncerramento { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public string ObservacoesEncerramento { get; set; } = string.Empty;
        public DateTime? DataAprovacao { get; set; }
        public bool? Aprovada { get; set; }
        public string Aprovador { get; set; } = string.Empty;
        public string ObservacoesAprovacao { get; set; } = string.Empty;
        public bool AprovacaoPendente { get; set; }
        public int? SatisfacaoNota { get; set; }
        public string SatisfacaoComentario { get; set; } = string.Empty;
        public DateTime? DataAvaliacao { get; set; }
        public bool AvaliacaoPendente { get; set; }
        public int Userid { get; set; }

    }
}
