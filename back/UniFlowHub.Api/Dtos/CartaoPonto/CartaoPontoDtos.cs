namespace UniFlowHub.Api.Dtos.CartaoPonto
{
    public class CartaoPontoArquivoResponseDto
    {
        public int Id { get; set; }
        public string NomeArquivo { get; set; } = string.Empty;
        public string CnpjUnidade { get; set; } = string.Empty;
        public string UnidadeNome { get; set; } = string.Empty;
        public DateTime DataImportacao { get; set; }
        public int TotalRegistros { get; set; }
        public int TotalFuncionarios { get; set; }
    }

    public class CartaoPontoFuncionarioResponseDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string CnpjUnidade { get; set; } = string.Empty;
        public string UnidadeNome { get; set; } = string.Empty;
        public int TotalRegistros { get; set; }
        public int TotalDias { get; set; }
        public bool ConfirmadoPeloUsuario { get; set; }
        public bool PrecisaAjuste { get; set; }
    }

    public class CartaoPontoRegistroResponseDto
    {
        public int Id { get; set; }
        public int ArquivoId { get; set; }
        public string FuncionarioNome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public DateTime Data { get; set; }
        public string HorarioOriginal { get; set; } = string.Empty;
        public string HorarioEditado { get; set; } = string.Empty;
        public int Sequencia { get; set; }
        public bool ConfirmadoPeloUsuario { get; set; }
        public bool PrecisaAjuste { get; set; }
    }

    public class CartaoPontoRegistroUpdateDto
    {
        public string? HorarioEditado { get; set; }
    }

    public class CartaoPontoRespostaUsuarioDto
    {
        public bool PrecisaAjuste { get; set; }
    }
}
