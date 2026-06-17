using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class BaseConhecimentoTI
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string ArquivoNome { get; set; } = string.Empty;
        public string ArquivoUrl { get; set; } = string.Empty;
        public string ArquivoContentType { get; set; } = string.Empty;
        public DateTime DataCadastro { get; set; }
        public DateTime? DataAtualizacao { get; set; }
        public int Userid { get; set; }
        public Users? OwnerUser { get; set; }
    }
}
