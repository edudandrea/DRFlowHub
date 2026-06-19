namespace UniFlowHub.Api.Dtos.Repasses
{
    public class RepasseVendasVendedorFilterDto : RepasseDashboardFilterDto
    {
        public string? Vendedor { get; set; }
    }

    public class RepasseVendasVendedorDto
    {
        public List<RepasseVendedorResumoDto> Vendedores { get; set; } = new();
        public List<RepasseVendaVendedorItemDto> Vendas { get; set; } = new();
        public DateTime AtualizadoEm { get; set; } = DateTime.Now;
    }

    public class RepasseVendedorResumoDto
    {
        public string Vendedor { get; set; } = string.Empty;
        public string Filial { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal TotalVenda { get; set; }
        public decimal Margem { get; set; }
    }

    public class RepasseVendaVendedorItemDto
    {
        public int Empresa { get; set; }
        public int Revenda { get; set; }
        public string NomeRevenda { get; set; } = string.Empty;
        public string Vendedor { get; set; } = string.Empty;
        public string Veiculo { get; set; } = string.Empty;
        public string Placa { get; set; } = string.Empty;
        public string NumeroNotaFiscal { get; set; } = string.Empty;
        public decimal TotalVenda { get; set; }
        public decimal Margem { get; set; }
        public DateTime DataVenda { get; set; }
    }
}
