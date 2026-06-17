namespace UniFlowHub.Api.Dtos.Unidades
{
    public class UnidadeCreateDto
    {
        public string Nome { get; set; } = string.Empty;
        public int EmpresaId { get; set; }
        public int NumeroRevenda { get; set; }
        public string Empresa { get; set; } = string.Empty;
        public string Revenda { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;
    }
}
