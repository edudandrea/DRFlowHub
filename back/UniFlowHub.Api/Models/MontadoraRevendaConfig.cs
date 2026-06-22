using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class MontadoraRevendaConfig
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int EmpresaNumero { get; set; }
        public int RevendaNumero { get; set; }
        public string Montadora { get; set; } = string.Empty;
        public string? LogoMontadoraUrl { get; set; }
        public bool Ativa { get; set; } = true;
        public DateTime DataAtualizacao { get; set; }
    }
}
