namespace UniFlowHub.Api.Dtos.SolicitacoesRH
{
    public class SolicitacoesRHUpdateDto
    {
        public string Unidade { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string TipoSolicitacao { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string AnexossUrl { get; set; } = string.Empty;
        public string Prioridade { get; set; } = string.Empty;
        public string Responsavel { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
