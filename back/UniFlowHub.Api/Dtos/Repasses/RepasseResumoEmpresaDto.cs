namespace UniFlowHub.Api.Dtos.Repasses
{
    public class RepasseResumoEmpresaDto
    {
        public int Empresa { get; set; }
        public string NomeEmpresa { get; set; } = string.Empty;
        public int VolumeDe { get; set; }
        public int VolumePara { get; set; }
        public decimal CustoDe { get; set; }
        public decimal CustoPara { get; set; }
        public decimal TicketMedio { get; set; }
        public int MediaGiroEstoque { get; set; }
        public decimal Distorcao { get; set; }
        public decimal LimiteAutorizado { get; set; }
    }
}
