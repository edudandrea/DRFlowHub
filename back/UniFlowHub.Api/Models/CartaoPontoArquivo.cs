using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class CartaoPontoArquivo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string NomeArquivo { get; set; } = string.Empty;
        public string CnpjUnidade { get; set; } = string.Empty;
        public int? UnidadeId { get; set; }
        public Unidade? Unidade { get; set; }
        public DateTime DataImportacao { get; set; }
        public int ImportadoPorUserId { get; set; }
        public Users? ImportadoPorUser { get; set; }
        public ICollection<CartaoPontoRegistro> Registros { get; set; } = new List<CartaoPontoRegistro>();
    }
}
