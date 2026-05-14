namespace DRFlowHub.Api.Dtos.Unidades
{
    public class EmpresaCreateDto
    {
        public int Numero { get; set; }
        public string Nome { get; set; } = string.Empty;
    }

    public class EmpresaResponseDto
    {
        public int Id { get; set; }
        public int Numero { get; set; }
        public string Nome { get; set; } = string.Empty;
        public DateTime DataCadastro { get; set; }
    }
}
