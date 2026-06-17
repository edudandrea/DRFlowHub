using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class PerfilSistema
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public bool PadraoSistema { get; set; }
        public ICollection<PerfilSistemaAcesso> Acessos { get; set; } = new List<PerfilSistemaAcesso>();
        public ICollection<PerfilSistemaEmpresa> Empresas { get; set; } = new List<PerfilSistemaEmpresa>();
    }
}
