namespace UniFlowHub.Api.Dtos.SolicitacoesCompra
{
    public class SolicitacaoCompraUpdateDto
    {
        public string Titulo { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public string Unidade { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public decimal ValorEstimado { get; set; }
        public string FornecedorSugerido { get; set; } = string.Empty;
        public string Prioridade { get; set; } = string.Empty;
        public string Justificativa { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Comprador { get; set; } = string.Empty;
        public string ObservacoesCompras { get; set; } = string.Empty;
    }
}
