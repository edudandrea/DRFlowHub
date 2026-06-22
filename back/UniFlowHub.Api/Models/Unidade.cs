using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFlowHub.Api.Models
{
    public class Unidade
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        
        /// <summary>
        /// Referência à empresa (ID do PostgreSQL) - DEPRECATED, usar dados do Oracle
        /// </summary>
        [Obsolete("Usar OracleEmpresasService para dados de empresa")]
        public int? EmpresaId { get; set; }
        
        /// <summary>
        /// Referência à empresa cadastrada - DEPRECATED
        /// </summary>
        [Obsolete("Usar OracleEmpresasService para dados de empresa")]
        public Empresa? EmpresaCadastro { get; set; }
        
        public int NumeroRevenda { get; set; }
        public string Empresa { get; set; } = string.Empty;
        public string Revenda { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;
        
        /// <summary>
        /// Montadora que a empresa/revenda pertence
        /// </summary>
        public string Montadora { get; set; } = string.Empty;
        
        /// <summary>
        /// URL da logo da montadora (local file ou cloud URL)
        /// </summary>
        public string? LogoMontadoraUrl { get; set; }
        
        public DateTime DataCadastro { get; set; }
        public ICollection<Users> Usuarios { get; set; } = new List<Users>();
    }
}
