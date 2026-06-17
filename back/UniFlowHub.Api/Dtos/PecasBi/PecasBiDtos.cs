namespace UniFlowHub.Api.Dtos.PecasBi
{
    public class PecasBiFilterDto
    {
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public DateTime? RankingDataInicio { get; set; }
        public DateTime? RankingDataFim { get; set; }
        public string? Empresa { get; set; }
        public string? Revenda { get; set; }
        public string? Canal { get; set; }
    }

    public class PecasBiResponseDto
    {
        public DateTime AtualizadoEm { get; set; }
        public bool PodeVerRankingVendedores { get; set; }
        public List<int>? EmpresasPermitidas { get; set; }
        public List<int>? RevendasPermitidas { get; set; }
        public List<PecaVendaMensalDto> VendasMensais { get; set; } = new();
        public List<PecaCategoriaDto> Categorias { get; set; } = new();
        public List<PecaRankingDto> Pecas { get; set; } = new();
        public List<PecaVendedorDto> Vendedores { get; set; } = new();
        public List<PecaCanalDto> Canais { get; set; } = new();
        public List<PecaClienteDto> Clientes { get; set; } = new();
        public List<PecaClienteDto> Seguradoras { get; set; } = new();
        public PecaMetaResumoDto? MinhaMeta { get; set; }
    }

    public class PecaVendaMensalDto
    {
        public string Mes { get; set; } = string.Empty;
        public decimal Faturamento { get; set; }
        public decimal Margem { get; set; }
        public decimal Rentabilidade { get; set; }
        public decimal RentabilidadePercentual { get; set; }
        public int Quantidade { get; set; }
    }

    public class PecaCategoriaDto
    {
        public string Nome { get; set; } = string.Empty;
        public decimal Faturamento { get; set; }
        public decimal MargemPercentual { get; set; }
    }

    public class PecaRankingDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal Faturamento { get; set; }
        public decimal MargemPercentual { get; set; }
        public decimal Rentabilidade { get; set; }
        public decimal RentabilidadePercentual { get; set; }
        public int GiroDias { get; set; }
    }

    public class PecaVendedorDto
    {
        public string Nome { get; set; } = string.Empty;
        public string CpfVendedor { get; set; } = string.Empty;
        public decimal Faturamento { get; set; }
        public int Pedidos { get; set; }
        public decimal ConversaoPercentual { get; set; }
        public decimal MetaVendas { get; set; }
        public decimal MetaAtual { get; set; }
        public decimal MetaMesAnterior { get; set; }
        public DateTime? MetaDataInicio { get; set; }
        public DateTime? MetaDataFim { get; set; }
        public List<PecaVendaDiariaVendedorDto> VendasDiarias { get; set; } = new();
        public List<PecaVendedorMetaMensalDto> MetasMensais { get; set; } = new();
    }

    public class PecaVendaDiariaVendedorDto
    {
        public DateTime Data { get; set; }
        public decimal Faturamento { get; set; }
        public int Pedidos { get; set; }
    }

    public class PecaVendedorMetaMensalDto
    {
        public DateTime Mes { get; set; }
        public decimal ValorMeta { get; set; }
    }

    public class PecaCanalDto
    {
        public string Nome { get; set; } = string.Empty;
        public decimal Faturamento { get; set; }
        public int ClientesAtendidos { get; set; }
    }

    public class PecaCanalDetalheDto
    {
        public string Canal { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public string NumeroNotaFiscal { get; set; } = string.Empty;
        public DateTime Data { get; set; }
        public decimal Faturamento { get; set; }
    }

    public class PecaClienteDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public decimal Faturamento { get; set; }
        public int Notas { get; set; }
    }

    public class PecaMetaResumoDto
    {
        public decimal ValorVendido { get; set; }
        public decimal ValorMeta { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
    }

    public class PecaVendedorMetaDto
    {
        public string CpfVendedor { get; set; } = string.Empty;
        public string NomeVendedor { get; set; } = string.Empty;
        public decimal ValorMeta { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
    }
}
