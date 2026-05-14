using DRFlowHub.Api.Data;
using DRFlowHub.Api.Dtos.Perfis;
using DRFlowHub.Api.Models;
using DRFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace DRFlowHub.Api.Services
{
    public class PerfisService
    {
        private readonly AppDbContext _context;

        public static readonly List<AcessoSistemaDto> AcessosDisponiveis = new()
        {
            new() { Chave = "dashboard-admin", Nome = "Dashboard do administrador", Grupo = "Dashboard" },
            new() { Chave = "dashboard-rh", Nome = "Dashboard do RH", Grupo = "Dashboard" },
            new() { Chave = "rh", Nome = "Solicitacoes do RH", Grupo = "Departamentos" },
            new() { Chave = "rh-admin", Nome = "Administrador do setor RH", Grupo = "Administradores de setor" },
            new() { Chave = "cartao-ponto", Nome = "Controle cartao ponto", Grupo = "Departamentos" },
            new() { Chave = "ti", Nome = "Chamados de TI", Grupo = "Departamentos" },
            new() { Chave = "ti-admin", Nome = "Administrador do setor TI", Grupo = "Administradores de setor" },
            new() { Chave = "equipamentos-ti", Nome = "Equipamentos de TI", Grupo = "Departamentos" },
            new() { Chave = "compras", Nome = "Compras", Grupo = "Departamentos" },
            new() { Chave = "compras-admin", Nome = "Administrador do setor Compras", Grupo = "Administradores de setor" },
            new() { Chave = "controladoria", Nome = "Controladoria", Grupo = "Departamentos" },
            new() { Chave = "vendas-pecas", Nome = "BI venda de pecas", Grupo = "BI" },
            new() { Chave = "veiculos", Nome = "Estoque de veiculos", Grupo = "Veiculos" },
            new() { Chave = "veiculos-bi", Nome = "BI venda de veiculos", Grupo = "BI" },
            new() { Chave = "usuarios", Nome = "Cadastro de usuarios", Grupo = "Cadastros" },
            new() { Chave = "empresas-revendas", Nome = "Cadastro de empresas e revendas", Grupo = "Cadastros" },
            new() { Chave = "perfis", Nome = "Cadastro de perfil", Grupo = "Cadastros" },
        };

        private static readonly Dictionary<string, string[]> PerfisPadrao = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = AcessosDisponiveis.Select(a => a.Chave).ToArray(),
            ["TI"] = new[] { "dashboard-admin", "ti", "ti-admin", "equipamentos-ti", "controladoria", "vendas-pecas", "veiculos", "veiculos-bi", "usuarios", "empresas-revendas", "perfis" },
            ["RH"] = new[] { "dashboard-rh", "rh", "rh-admin", "cartao-ponto" },
            ["Diretoria"] = new[] { "compras" },
            ["Compras"] = new[] { "compras", "compras-admin" },
            ["Controladoria"] = new[] { "controladoria" },
            ["Qualidade Nissan"] = new[] { "veiculos", "veiculos-bi" },
            ["Gerente Geral de Pecas"] = new[] { "vendas-pecas" },
            ["Gerente de Pecas"] = new[] { "vendas-pecas" },
            ["Vendedor de Pecas"] = new[] { "vendas-pecas" },
            ["Gestor"] = Array.Empty<string>(),
            ["Usuario"] = Array.Empty<string>(),
        };

        public PerfisService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<AcessoSistemaDto>> ListAcessosAsync()
        {
            await EnsureDefaultsAsync();
            return AcessosDisponiveis;
        }

        public async Task<List<PerfilSistemaDto>> ListAsync(string role)
        {
            EnsureCanManage(role);
            await EnsureDefaultsAsync();
            return await _context.PerfilSistema
                .Include(p => p.Acessos)
                .OrderBy(p => p.Nome)
                .Select(p => new PerfilSistemaDto
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    PadraoSistema = p.PadraoSistema,
                    Acessos = p.Acessos.Select(a => a.Chave).OrderBy(a => a).ToList()
                })
                .ToListAsync();
        }

        public async Task<PerfilSistemaDto> SaveAsync(string role, PerfilSistemaSaveDto dto)
        {
            EnsureCanManage(role);
            await EnsureDefaultsAsync();

            var nome = dto.Nome.Trim();
            if (string.IsNullOrWhiteSpace(nome))
                throw new InvalidOperationException("Nome do perfil e obrigatorio.");

            var validKeys = AcessosDisponiveis.Select(a => a.Chave).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var acessos = dto.Acessos.Where(validKeys.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var perfil = await _context.PerfilSistema.Include(p => p.Acessos).FirstOrDefaultAsync(p => p.Nome.ToLower() == nome.ToLower());
            if (perfil is null)
            {
                perfil = new PerfilSistema { Nome = nome, PadraoSistema = false };
                _context.PerfilSistema.Add(perfil);
            }
            else
            {
                perfil.Nome = nome;
                _context.PerfilSistemaAcesso.RemoveRange(perfil.Acessos);
            }

            foreach (var acesso in acessos)
                perfil.Acessos.Add(new PerfilSistemaAcesso { Chave = acesso });

            await _context.SaveChangesAsync();
            return new PerfilSistemaDto { Id = perfil.Id, Nome = perfil.Nome, PadraoSistema = perfil.PadraoSistema, Acessos = acessos };
        }

        public async Task<List<string>> GetAcessosByPerfilAsync(string role)
        {
            await EnsureDefaultsAsync();
            var normalized = NormalizePerfilName(role);
            return await _context.PerfilSistema
                .Where(p => p.Nome == normalized)
                .SelectMany(p => p.Acessos.Select(a => a.Chave))
                .ToListAsync();
        }

        public async Task<bool> PerfilExistsAsync(string role)
        {
            await EnsureDefaultsAsync();
            var normalized = NormalizePerfilName(role);
            return await _context.PerfilSistema.AnyAsync(p => p.Nome == normalized);
        }

        public async Task EnsureDefaultsAsync()
        {
            foreach (var item in PerfisPadrao)
            {
                var nome = item.Key;
                var perfil = await _context.PerfilSistema.Include(p => p.Acessos).FirstOrDefaultAsync(p => p.Nome == nome);
                if (perfil is null)
                {
                    perfil = new PerfilSistema { Nome = nome, PadraoSistema = true };
                    _context.PerfilSistema.Add(perfil);
                    foreach (var acesso in item.Value)
                        perfil.Acessos.Add(new PerfilSistemaAcesso { Chave = acesso });
                }
                else if (perfil.PadraoSistema)
                {
                    var current = perfil.Acessos.Select(a => a.Chave).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var acesso in item.Value.Where(acesso => !current.Contains(acesso)))
                        perfil.Acessos.Add(new PerfilSistemaAcesso { Chave = acesso });
                }
            }

            await _context.SaveChangesAsync();
        }

        public static string NormalizePerfilName(string role)
        {
            var normalized = AuthService.NormalizeBuiltInRole(role);
            return string.IsNullOrWhiteSpace(normalized) ? role.Trim() : normalized;
        }

        private static void EnsureCanManage(string role)
        {
            if (!RoleScope.IsAdmin(role) && !RoleScope.IsTI(role))
                throw new UnauthorizedAccessException("Somente Admin ou TI podem gerenciar perfis.");
        }
    }
}
