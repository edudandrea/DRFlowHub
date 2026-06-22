using UniFlowHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace UniFlowHub.Api.Data
{
    public class AppDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor)
        : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<Users> User { get; set; }
        public DbSet<SolicitacoesRH> SolicitacaoRH { get; set; }
        public DbSet<SolicitacaoRHComunicacao> SolicitacaoRHComunicacao { get; set; }
        public DbSet<ChamadosTI> ChamadoTI { get; set; }
        public DbSet<ChamadoTIComunicacao> ChamadoTIComunicacao { get; set; }
        public DbSet<BaseConhecimentoTI> BaseConhecimentoTI { get; set; }
        public DbSet<EquipamentoTI> EquipamentoTI { get; set; }
        public DbSet<SolicitacaoCompra> SolicitacaoCompra { get; set; }
        public DbSet<SolicitacaoCompraComunicacao> SolicitacaoCompraComunicacao { get; set; }
        public DbSet<Unidade> Unidade { get; set; }
        public DbSet<Empresa> Empresa { get; set; }
        public DbSet<MontadoraRevendaConfig> MontadoraRevendaConfig { get; set; }
        public DbSet<EmpresaRevendaStatusConfig> EmpresaRevendaStatusConfig { get; set; }
        public DbSet<CartaoPontoArquivo> CartaoPontoArquivo { get; set; }
        public DbSet<CartaoPontoRegistro> CartaoPontoRegistro { get; set; }
        public DbSet<GuiaIcmsPagamento> GuiaIcmsPagamento { get; set; }
        public DbSet<VeiculoReserva> VeiculoReserva { get; set; }
        public DbSet<PecaVendedorMeta> PecaVendedorMeta { get; set; }
        public DbSet<PerfilSistema> PerfilSistema { get; set; }
        public DbSet<PerfilSistemaAcesso> PerfilSistemaAcesso { get; set; }
        public DbSet<PerfilSistemaEmpresa> PerfilSistemaEmpresa { get; set; }
        public DbSet<UserPerfil> UserPerfil { get; set; }
        public DbSet<GestaoPessoasEtapa> GestaoPessoasEtapa { get; set; }
        public DbSet<GestaoPessoasProcesso> GestaoPessoasProcesso { get; set; }
        public DbSet<GestaoPessoasProcessoHistorico> GestaoPessoasProcessoHistorico { get; set; }
        public DbSet<GestaoPessoasCargo> GestaoPessoasCargo { get; set; }
        public DbSet<GestaoPessoasCargoAcesso> GestaoPessoasCargoAcesso { get; set; }
        public DbSet<GestaoPessoasItem> GestaoPessoasItem { get; set; }
        public DbSet<GestaoPessoasCargoItem> GestaoPessoasCargoItem { get; set; }
        public DbSet<GestaoPessoasColaborador> GestaoPessoasColaborador { get; set; }
        public DbSet<GestaoPessoasColaboradorRetirada> GestaoPessoasColaboradorRetirada { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    property.SetColumnType("timestamp with time zone");
            }

            modelBuilder.Entity<Users>()
                .HasOne(u => u.CreatedByUser)
                .WithMany(u => u.CreatedUsers)
                .HasForeignKey(u => u.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Users>()
                .HasOne(u => u.Unidade)
                .WithMany(u => u.Usuarios)
                .HasForeignKey(u => u.UnidadeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<UserPerfil>()
                .HasIndex(s => new { s.UserId, s.Perfil })
                .IsUnique();

            modelBuilder.Entity<UserPerfil>()
                .HasOne(s => s.User)
                .WithMany(s => s.Perfis)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Empresa>()
                .HasIndex(e => e.Numero)
                .IsUnique();

            modelBuilder.Entity<Empresa>()
                .Property(e => e.LogoUrl)
                .HasColumnType("text");

            modelBuilder.Entity<MontadoraRevendaConfig>()
                .HasIndex(e => new { e.EmpresaNumero, e.RevendaNumero })
                .IsUnique();

            modelBuilder.Entity<MontadoraRevendaConfig>()
                .Property(e => e.LogoMontadoraUrl)
                .HasColumnType("text");

            modelBuilder.Entity<EmpresaRevendaStatusConfig>()
                .HasIndex(e => e.EmpresaNumero)
                .IsUnique();

            modelBuilder.Entity<Unidade>()
                .Property(u => u.LogoMontadoraUrl)
                .HasColumnType("text");

            modelBuilder.Entity<Unidade>()
                .HasOne(u => u.EmpresaCadastro)
                .WithMany(e => e.Revendas)
                .HasForeignKey(u => u.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SolicitacoesRH>()
                .HasOne(s => s.OwnerUser)
                .WithMany()
                .HasForeignKey(s => s.Userid)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SolicitacaoRHComunicacao>()
                .HasOne(s => s.SolicitacaoRH)
                .WithMany(s => s.Comunicacoes)
                .HasForeignKey(s => s.SolicitacaoRHId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChamadosTI>()
                .HasOne(s => s.OwnerUser)
                .WithMany()
                .HasForeignKey(s => s.Userid)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChamadoTIComunicacao>()
                .HasOne(s => s.ChamadoTI)
                .WithMany(s => s.Comunicacoes)
                .HasForeignKey(s => s.ChamadoTIId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BaseConhecimentoTI>()
                .HasOne(s => s.OwnerUser)
                .WithMany()
                .HasForeignKey(s => s.Userid)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CartaoPontoArquivo>()
                .HasOne(s => s.ImportadoPorUser)
                .WithMany()
                .HasForeignKey(s => s.ImportadoPorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CartaoPontoArquivo>()
                .HasOne(s => s.Unidade)
                .WithMany()
                .HasForeignKey(s => s.UnidadeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CartaoPontoRegistro>()
                .HasOne(s => s.Arquivo)
                .WithMany(s => s.Registros)
                .HasForeignKey(s => s.CartaoPontoArquivoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CartaoPontoRegistro>()
                .HasOne(s => s.EditadoPorUser)
                .WithMany()
                .HasForeignKey(s => s.EditadoPorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GuiaIcmsPagamento>()
                .HasIndex(s => s.GuiaId)
                .IsUnique();

            modelBuilder.Entity<GuiaIcmsPagamento>()
                .HasOne(s => s.AtualizadoPorUser)
                .WithMany()
                .HasForeignKey(s => s.AtualizadoPorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<VeiculoReserva>()
                .HasIndex(s => s.Chassi)
                .IsUnique();

            modelBuilder.Entity<VeiculoReserva>()
                .HasOne(s => s.AtualizadoPorUser)
                .WithMany()
                .HasForeignKey(s => s.AtualizadoPorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PecaVendedorMeta>()
                .HasIndex(s => s.CpfVendedor)
                .IsUnique(false);

            modelBuilder.Entity<PecaVendedorMeta>()
                .HasIndex(s => new { s.CpfVendedor, s.Origem, s.TipoMeta, s.DataInicio, s.DataFim })
                .IsUnique(false);

            modelBuilder.Entity<PecaVendedorMeta>()
                .HasOne(s => s.AtualizadoPorUser)
                .WithMany()
                .HasForeignKey(s => s.AtualizadoPorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PerfilSistema>()
                .HasIndex(s => s.Nome)
                .IsUnique();

            modelBuilder.Entity<PerfilSistemaAcesso>()
                .HasIndex(s => new { s.PerfilSistemaId, s.Chave })
                .IsUnique();

            modelBuilder.Entity<PerfilSistemaAcesso>()
                .HasOne(s => s.PerfilSistema)
                .WithMany(s => s.Acessos)
                .HasForeignKey(s => s.PerfilSistemaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PerfilSistemaEmpresa>()
                .HasIndex(s => new { s.PerfilSistemaId, s.EmpresaNumero })
                .IsUnique();

            modelBuilder.Entity<PerfilSistemaEmpresa>()
                .HasOne(s => s.PerfilSistema)
                .WithMany(s => s.Empresas)
                .HasForeignKey(s => s.PerfilSistemaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EquipamentoTI>()
                .HasOne(s => s.OwnerUser)
                .WithMany()
                .HasForeignKey(s => s.Userid)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SolicitacaoCompra>()
                .HasOne(s => s.OwnerUser)
                .WithMany()
                .HasForeignKey(s => s.Userid)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SolicitacaoCompraComunicacao>()
                .HasOne(s => s.SolicitacaoCompra)
                .WithMany(s => s.Comunicacoes)
                .HasForeignKey(s => s.SolicitacaoCompraId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GestaoPessoasEtapa>()
                .HasIndex(s => new { s.TipoProcesso, s.Ordem });

            modelBuilder.Entity<GestaoPessoasProcesso>()
                .HasOne(s => s.OwnerUser)
                .WithMany()
                .HasForeignKey(s => s.Userid)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GestaoPessoasProcesso>()
                .HasOne(s => s.EtapaAtual)
                .WithMany()
                .HasForeignKey(s => s.EtapaAtualId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<GestaoPessoasProcessoHistorico>()
                .HasOne(s => s.Processo)
                .WithMany(s => s.Historico)
                .HasForeignKey(s => s.ProcessoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GestaoPessoasProcessoHistorico>()
                .HasOne(s => s.Etapa)
                .WithMany()
                .HasForeignKey(s => s.EtapaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GestaoPessoasCargo>()
                .HasIndex(s => s.Nome)
                .IsUnique();

            modelBuilder.Entity<GestaoPessoasCargoAcesso>()
                .HasIndex(s => new { s.CargoId, s.Chave })
                .IsUnique();

            modelBuilder.Entity<GestaoPessoasCargoAcesso>()
                .HasOne(s => s.Cargo)
                .WithMany(s => s.Acessos)
                .HasForeignKey(s => s.CargoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GestaoPessoasItem>()
                .HasIndex(s => new { s.Tipo, s.Nome, s.Tamanho })
                .IsUnique(false);

            modelBuilder.Entity<GestaoPessoasCargoItem>()
                .HasIndex(s => new { s.CargoId, s.ItemId })
                .IsUnique();

            modelBuilder.Entity<GestaoPessoasCargoItem>()
                .HasOne(s => s.Cargo)
                .WithMany(s => s.Itens)
                .HasForeignKey(s => s.CargoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GestaoPessoasCargoItem>()
                .HasOne(s => s.Item)
                .WithMany(s => s.Cargos)
                .HasForeignKey(s => s.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GestaoPessoasColaborador>()
                .HasIndex(s => s.Cpf)
                .IsUnique(false);

            modelBuilder.Entity<GestaoPessoasColaborador>()
                .HasOne(s => s.Cargo)
                .WithMany(s => s.Colaboradores)
                .HasForeignKey(s => s.CargoId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<GestaoPessoasColaborador>()
                .HasOne(s => s.Unidade)
                .WithMany()
                .HasForeignKey(s => s.UnidadeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<GestaoPessoasColaboradorRetirada>()
                .HasOne(s => s.Colaborador)
                .WithMany(s => s.Retiradas)
                .HasForeignKey(s => s.ColaboradorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GestaoPessoasColaboradorRetirada>()
                .HasOne(s => s.Item)
                .WithMany(s => s.Retiradas)
                .HasForeignKey(s => s.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
