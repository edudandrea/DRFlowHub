namespace UniFlowHub.Api.Dtos.SolicitacoesCompra
{
    public class SolicitacaoCompraComunicacaoResponseDto
    {
        public int Id { get; set; }
        public int SolicitacaoCompraId { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public string AutorNome { get; set; } = string.Empty;
        public string AutorRole { get; set; } = string.Empty;
        public int AutorUserId { get; set; }
        public DateTime DataCriacao { get; set; }
    }
}
