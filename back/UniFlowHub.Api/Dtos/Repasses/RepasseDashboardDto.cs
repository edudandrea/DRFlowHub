namespace UniFlowHub.Api.Dtos.Repasses
{
    public class RepasseDashboardDto
    {
        public List<RepasseVeiculoDto> Veiculos { get; set; } = new();
        public List<RepasseVeiculoDto> TopDiasEstoque { get; set; } = new();
        public List<RepasseResumoEmpresaDto> Resumos { get; set; } = new();
    }
}
