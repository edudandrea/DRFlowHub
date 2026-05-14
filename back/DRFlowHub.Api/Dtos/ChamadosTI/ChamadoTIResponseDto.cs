namespace DRFlowHub.Api.Dtos.ChamadosTI
{
    public class ChamadoTIResponseDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public string Unidade { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string Prioridade { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Responsavel { get; set; } = string.Empty;
        public string AcessoRemotoUrl { get; set; } = string.Empty;
        public string RustDeskId { get; set; } = string.Empty;
        public string RustDeskSenha { get; set; } = string.Empty;
        public string RustDeskServidor { get; set; } = string.Empty;
        public string RustDeskKey { get; set; } = string.Empty;
        public string EquipamentoNome { get; set; } = string.Empty;
        public string EquipamentoIp { get; set; } = string.Empty;
        public string EquipamentoSistemaOperacional { get; set; } = string.Empty;
        public string AnexoImagemUrl { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public string ObservacoesEncerramento { get; set; } = string.Empty;
        public int? SatisfacaoNota { get; set; }
        public string SatisfacaoComentario { get; set; } = string.Empty;
        public DateTime? DataAvaliacao { get; set; }
        public bool AvaliacaoPendente { get; set; }
        public DateTime DataAbertura { get; set; }
        public DateTime? DataPrimeiroEncerramento { get; set; }
        public DateTime? DataReabertura { get; set; }
        public DateTime? DataEncerramento { get; set; }
        public DateTime UltimaMovimentacao { get; set; }
        public bool Reaberto { get; set; }
        public int Userid { get; set; }
    }
}
