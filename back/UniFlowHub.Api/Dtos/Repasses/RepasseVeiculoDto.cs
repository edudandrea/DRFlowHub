namespace UniFlowHub.Api.Dtos.Repasses
{
    public class RepasseVeiculoDto
    {
        public int Empresa { get; set; }
        public int Revenda { get; set; }
        public string NomeEmpresa { get; set; } = string.Empty;
        public string NomeRevenda { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string Placa { get; set; } = string.Empty;
        public decimal CustoContabil { get; set; }
        public string Situacao { get; set; } = string.Empty;
        public int DiasEstoque { get; set; }
    }
}
