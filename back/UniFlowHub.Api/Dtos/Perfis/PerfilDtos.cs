namespace UniFlowHub.Api.Dtos.Perfis
{
    public class AcessoSistemaDto
    {
        public string Chave { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Grupo { get; set; } = string.Empty;
    }

    public class PerfilSistemaDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public bool PadraoSistema { get; set; }
        public List<string> Acessos { get; set; } = new();
        public List<int> Empresas { get; set; } = new();
    }

    public class PerfilSistemaSaveDto
    {
        public int? Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public List<string> Acessos { get; set; } = new();
        public List<int> Empresas { get; set; } = new();
    }
}
