using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UniFlowHub.Api.Dtos.SolicitacoesRH
{
    public class SolicitacoesRHCreateDto
    {
        public string Unidade { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string TipoSolicitacao { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string? AnexossUrl { get; set; }
        public string  Prioridade { get; set; } = string.Empty;
        public string? Responsavel { get; set; }
        public DateTime? DataSolicitacao { get; set; }
        public string? Observacoes { get; set; }
        public string Status { get; set; } = string.Empty;
        public int Userid { get; set; }
        public IFormFile? Anexo { get; set; }
    }
}
