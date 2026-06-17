using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class CartaoPontoRegistro
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int CartaoPontoArquivoId { get; set; }
        public CartaoPontoArquivo? Arquivo { get; set; }
        public string FuncionarioNome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public DateTime Data { get; set; }
        public string HorarioOriginal { get; set; } = string.Empty;
        public string HorarioEditado { get; set; } = string.Empty;
        public int Sequencia { get; set; }
        public string LinhaOriginal { get; set; } = string.Empty;
        public bool ConfirmadoPeloUsuario { get; set; }
        public bool PrecisaAjuste { get; set; }
        public DateTime? DataRespostaUsuario { get; set; }
        public DateTime? DataEdicao { get; set; }
        public int? EditadoPorUserId { get; set; }
        public Users? EditadoPorUser { get; set; }
    }
}
