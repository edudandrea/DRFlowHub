using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class GestaoPessoasCargoAcesso
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int CargoId { get; set; }
        public GestaoPessoasCargo? Cargo { get; set; }
        public string Chave { get; set; } = string.Empty;
    }
}
