namespace UniFlowHub.Api.Dtos.Controladoria
{
    public class GuiaIcmsResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Documento { get; set; } = string.Empty;
        public string Empresa { get; set; } = string.Empty;
        public string Revenda { get; set; } = string.Empty;
        public string NumeroNota { get; set; } = string.Empty;
        public string Transacao { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string Competencia { get; set; } = string.Empty;
        public DateTime? DataVencimento { get; set; }
        public DateTime? DataPagamento { get; set; }
        public decimal Valor { get; set; }
        public decimal Difal { get; set; }
        public decimal Fcp { get; set; }
        public string Uf { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
    }

    public class GuiaIcmsFilterDto
    {
        public string? Empresa { get; set; }
        public string? Revenda { get; set; }
        public string? Transacao { get; set; }
        public string? Uf { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
    }

    public class GuiaIcmsPagamentoUpdateDto
    {
        public string Status { get; set; } = "Pago";
    }

    public class GuiaIcmsPagamentoLoteDto
    {
        public List<string> GuiaIds { get; set; } = new();
        public string Status { get; set; } = "Pago";
    }

    public class GuiaIcmsPagamentoLoteResponseDto
    {
        public int Atualizadas { get; set; }
        public string Status { get; set; } = "Pago";
        public DateTime? DataPagamento { get; set; }
    }
}
