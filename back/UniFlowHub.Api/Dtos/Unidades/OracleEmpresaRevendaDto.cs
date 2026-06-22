namespace UniFlowHub.Api.Dtos.Unidades
{
    /// <summary>
    /// DTO para empresa vinda do Oracle (GER_REVENDA)
    /// </summary>
    public class OracleEmpresaDto
    {
        public int Numero { get; set; }
        public string Nome { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para revenda vinda do Oracle (GER_REVENDA)
    /// </summary>
    public class OracleRevendaDto
    {
        public int EmpresaNumero { get; set; }
        public string EmpresaNome { get; set; } = string.Empty;
        public int NumeroRevenda { get; set; }
        public string NomeRevenda { get; set; } = string.Empty;
        public string RazaoSocial { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;
        public string Montadora { get; set; } = string.Empty;
        public string? LogoMontadoraUrl { get; set; }
    }

    /// <summary>
    /// DTO combinado para listagem de revendas com dados do PostgreSQL (logo/montadora)
    /// </summary>
    public class UnidadeOracleResponseDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int EmpresaNumero { get; set; }
        public string EmpresaNome { get; set; } = string.Empty;
        public int NumeroRevenda { get; set; }
        public string NomeRevenda { get; set; } = string.Empty;
        public string RazaoSocial { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;
        public string Montadora { get; set; } = string.Empty;
        public string? LogoMontadoraUrl { get; set; }
        public bool Ativa { get; set; } = true;
        public bool EmpresaAtiva { get; set; } = true;
        public bool RevendaAtiva { get; set; } = true;
    }
}
