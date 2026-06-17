using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class PerfilSistemaEmpresa
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int PerfilSistemaId { get; set; }
        public PerfilSistema? PerfilSistema { get; set; }
        public int EmpresaNumero { get; set; }
    }
}
