using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class Empresa
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int Numero { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public DateTime DataCadastro { get; set; }
        public ICollection<Unidade> Revendas { get; set; } = new List<Unidade>();
    }
}
