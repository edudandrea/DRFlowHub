namespace UniFlowHub.Api.Dtos.Repasses
{
    public class RepasseDashboardFilterDto
    {
        public int? Empresa { get; set; }
        public int? Revenda { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
    }
}
