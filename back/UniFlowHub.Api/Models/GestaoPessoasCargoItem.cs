using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class GestaoPessoasCargoItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int CargoId { get; set; }
        public GestaoPessoasCargo? Cargo { get; set; }
        public int ItemId { get; set; }
        public GestaoPessoasItem? Item { get; set; }
        public int Quantidade { get; set; } = 1;
        public bool Obrigatorio { get; set; } = true;
    }
}
