namespace UniFlowHub.Api.Dtos.VeiculosBi
{
    public class VeiculosBiFilterDto
    {
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public string? Empresa { get; set; }
        public string? Revenda { get; set; }
    }

    public class VeiculoAcessorioRankingDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string CpfVendedor { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal Faturamento { get; set; }
        public decimal MargemPercentual { get; set; }
        public decimal Rentabilidade { get; set; }
        public decimal Meta { get; set; }
        public string TipoMeta { get; set; } = "valor";
        public DateTime? MetaDataInicio { get; set; }
        public DateTime? MetaDataFim { get; set; }
    }

    public class VeiculosBiDashboardDto
    {
        public List<VeiculoBiFilialVendaDto> Filiais { get; set; } = new();
        public List<VeiculoBiVendaDiariaDto> VendasDiarias { get; set; } = new();
        public List<VeiculoBiVendaDetalheDto> VendasDetalhes { get; set; } = new();
        public List<VeiculoBiModeloRankingDto> Modelos { get; set; } = new();
        public List<VeiculoBiVendedorMetaDto> Vendedores { get; set; } = new();
        public DateTime AtualizadoEm { get; set; } = DateTime.Now;
    }

    public class VeiculosBiRetornoFiDashboardDto
    {
        public int Contratos { get; set; }
        public decimal RetornoTotal { get; set; }
        public decimal ValorFinanciado { get; set; }
        public decimal ValorVenda { get; set; }
        public decimal ComissaoTotal { get; set; }
        public List<VeiculosBiRetornoFiGrupoDto> Financeiras { get; set; } = new();
        public List<VeiculosBiRetornoFiGrupoDto> Vendedores { get; set; } = new();
        public List<VeiculosBiRetornoFiGrupoDto> Parcelas { get; set; } = new();
        public DateTime AtualizadoEm { get; set; } = DateTime.Now;
    }

    public class VeiculosBiRetornoFiGrupoDto
    {
        public string Nome { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal Retorno { get; set; }
        public decimal ValorFinanciado { get; set; }
        public decimal Comissao { get; set; }
    }

    public class VeiculoBiFilialVendaDto
    {
        public int EmpresaNumero { get; set; }
        public string EmpresaNome { get; set; } = string.Empty;
        public int RevendaNumero { get; set; }
        public string Filial { get; set; } = string.Empty;
        public int MetaNovos { get; set; }
        public int MetaVendaDireta { get; set; }
        public int AnunciadosNovos { get; set; }
        public int FaturadosNovos { get; set; }
        public int AnunciadosDireta { get; set; }
        public int FaturadosDireta { get; set; }
        public int Seminovos { get; set; }
        public int Propostas { get; set; }
        public int Baixados { get; set; }
        public decimal Faturamento { get; set; }
        public decimal Margem { get; set; }
        public decimal FaturamentoSemDireta { get; set; }
        public decimal MargemSemDireta { get; set; }
    }

    public class VeiculoBiVendaDiariaDto
    {
        public string Data { get; set; } = string.Empty;
        public int Novos { get; set; }
        public int VendaDireta { get; set; }
        public int Seminovos { get; set; }
    }

    public class VeiculoBiVendaDetalheDto
    {
        public string Data { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public string NotaFiscal { get; set; } = string.Empty;
        public string Veiculo { get; set; } = string.Empty;
        public decimal Valor { get; set; }
    }

    public class VeiculoBiModeloRankingDto
    {
        public string Modelo { get; set; } = string.Empty;
        public string Familia { get; set; } = string.Empty;
        public int Unidades { get; set; }
        public decimal Faturamento { get; set; }
        public decimal MargemPercentual { get; set; }
    }

    public class VeiculoBiVendedorMetaDto
    {
        public string Vendedor { get; set; } = string.Empty;
        public string CpfVendedor { get; set; } = string.Empty;
        public string Filial { get; set; } = string.Empty;
        public int Meta { get; set; }
        public string TipoMeta { get; set; } = "valor";
        public int Realizado { get; set; }
        public decimal Faturamento { get; set; }
        public DateTime? MetaDataInicio { get; set; }
        public DateTime? MetaDataFim { get; set; }
    }

    public class VeiculoVendedorMetaDto
    {
        public string CpfVendedor { get; set; } = string.Empty;
        public string NomeVendedor { get; set; } = string.Empty;
        public string Origem { get; set; } = "veiculos";
        public string TipoMeta { get; set; } = "valor";
        public decimal ValorMeta { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
    }
}
