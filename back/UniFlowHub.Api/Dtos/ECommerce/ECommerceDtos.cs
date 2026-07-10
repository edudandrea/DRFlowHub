namespace UniFlowHub.Api.Dtos.ECommerce
{
    public class ECommerceFilterDto
    {
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public string? Empresa { get; set; }
        public string? Revenda { get; set; }
    }

    public class ECommerceDashboardDto
    {
        public DateTime AtualizadoEm { get; set; }
        public List<ECommerceUnitDto> Unidades { get; set; } = new();
        public List<ECommerceAnnualSaleDto> EvolucaoAnual { get; set; } = new();
        public List<ECommerceMonthlySaleDto> EvolucaoMensal { get; set; } = new();
    }

    public class ECommerceUnitDto
    {
        public int EmpresaNumero { get; set; }
        public int RevendaNumero { get; set; }
        public int? VendedorCodigo { get; set; }
        public string VendedorNome { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string NomeCurto { get; set; } = string.Empty;
        public decimal Realizado { get; set; }
        public int NotasEmitidas { get; set; }
        public decimal TicketMedio { get; set; }
        public decimal Custo { get; set; }
        public decimal Impostos { get; set; }
        public decimal Despesas { get; set; }
        public decimal MargemContribuicaoValor { get; set; }
        public decimal MargemContribuicaoPercentual { get; set; }
        public decimal RentabilidadeValor { get; set; }
        public decimal RentabilidadePercentual { get; set; }
    }

    public class ECommerceAnnualSaleDto
    {
        public int Ano { get; set; }
        public decimal Realizado { get; set; }
        public int NotasEmitidas { get; set; }
        public decimal MargemContribuicaoValor { get; set; }
        public decimal MargemContribuicaoPercentual { get; set; }
    }

    public class ECommerceMonthlySaleDto
    {
        public int Ano { get; set; }
        public int Mes { get; set; }
        public decimal Realizado { get; set; }
        public int NotasEmitidas { get; set; }
        public decimal MargemContribuicaoValor { get; set; }
        public decimal MargemContribuicaoPercentual { get; set; }
    }

    public class ECommerceSpreadsheetImportDto
    {
        public ECommerceDashboardDto Dashboard { get; set; } = new();
        public int LinhasImportadas { get; set; }
        public decimal MargemContribuicaoValor { get; set; }
    }
}
