namespace UniFlowHub.Api.Dtos.SolicitacoesRH
{
    public class SolicitacaoRHComunicacaoResponseDto
    {
        public int Id { get; set; }
        public int SolicitacaoRHId { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public string AutorNome { get; set; } = string.Empty;
        public string AutorRole { get; set; } = string.Empty;
        public int AutorUserId { get; set; }
        public DateTime DataCriacao { get; set; }
    }
}
