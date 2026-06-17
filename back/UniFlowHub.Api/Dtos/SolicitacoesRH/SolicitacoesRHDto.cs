using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UniFlowHub.Api.Dtos.SolicitacoesRH
{
    public class SolicitacoesRHDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string TipoSolicitacao { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string DocumentosUrl { get; set; } = string.Empty;
        public DateTime DataSolicitacao { get; set; }
    }
}