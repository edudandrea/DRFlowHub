namespace UniFlowHub.Api.Dtos.Veiculos
{
    public class VeiculoEstoqueResponseDto
    {
        public string Chassi { get; set; } = string.Empty;
        public string CodigoVeiculo { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string DescricaoModelo { get; set; } = string.Empty;
        public string Cor { get; set; } = string.Empty;
        public string DescricaoCor { get; set; } = string.Empty;
        public int Empresa { get; set; }
        public int Revenda { get; set; }
        public bool Reservado { get; set; }
        public string OrigemReserva { get; set; } = string.Empty;
        public DateTime? DataReserva { get; set; }
    }

    public class VeiculoEstoqueFilterDto
    {
        public int? Empresa { get; set; }
        public int? Revenda { get; set; }
        public string? Busca { get; set; }
        public string? Reservado { get; set; }
    }

    public class VeiculoReservaUpdateDto
    {
        public int Empresa { get; set; }
        public bool Reservado { get; set; }
    }
}
