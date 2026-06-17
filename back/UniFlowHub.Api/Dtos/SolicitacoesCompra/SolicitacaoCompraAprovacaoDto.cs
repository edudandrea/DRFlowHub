namespace UniFlowHub.Api.Dtos.SolicitacoesCompra
{
    public class SolicitacaoCompraAprovacaoDto
    {
        public bool Aprovada { get; set; }
        public string ObservacoesAprovacao { get; set; } = string.Empty;
    }
}
