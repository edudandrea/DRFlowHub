namespace UniFlowHub.Api.Dtos.EquipamentosTI
{
    public class EquipamentoTIUpdateDto
    {
        public string Tipo { get; set; } = string.Empty;
        public string Patrimonio { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string Serial { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Origem { get; set; } = string.Empty;
        public string Destino { get; set; } = string.Empty;
        public string Responsavel { get; set; } = string.Empty;
        public int? FilialCompraId { get; set; }
        public string FilialCompra { get; set; } = string.Empty;
        public string NotaFiscalCompra { get; set; } = string.Empty;
        public int? UsuarioResponsavelId { get; set; }
        public string UsuarioResponsavelNome { get; set; } = string.Empty;
        public string UsuarioResponsavelEmail { get; set; } = string.Empty;
        public string UsuarioResponsavelDepartamento { get; set; } = string.Empty;
        public string UsuarioResponsavelUnidade { get; set; } = string.Empty;
        public DateTime? DataPrevistaRetorno { get; set; }
        public string Observacoes { get; set; } = string.Empty;
    }
}
