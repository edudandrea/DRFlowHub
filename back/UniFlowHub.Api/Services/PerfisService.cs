using UniFlowHub.Api.Data;
using UniFlowHub.Api.Dtos.Perfis;
using UniFlowHub.Api.Models;
using UniFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace UniFlowHub.Api.Services
{
    public class PerfisService
    {
        private readonly AppDbContext _context;
        private static readonly HashSet<string> LegacyPecasBiAcessos = new(StringComparer.OrdinalIgnoreCase)
        {
            "pecas-bi-renault",
            "pecas-bi-nissan",
            "pecas-bi-gm",
            "pecas-bi-fiat",
            "pecas-bi-bajaj",
            "pecas-bi-peugeot-citroen",
            "pecas-bi-mg",
            "pecas-bi-geely",
        };
        private static readonly HashSet<string> AcessosPadraoOcultos = new(StringComparer.OrdinalIgnoreCase)
        {
            "ti",
            "rh",
            "compras",
            "ti-admin",
            "rh-admin",
            "usuarios",
            "empresas-revendas",
            "perfis",
        };

        public static readonly List<AcessoSistemaDto> AcessosDisponiveis = new()
        {
            new() { Chave = "dashboard-admin", Nome = "Dashboard administrador", Grupo = "Dashboard" },
            new() { Chave = "dashboard-rh", Nome = "Dashboard RH", Grupo = "Dashboard" },
            new() { Chave = "rh", Nome = "Solicitações do RH", Grupo = "Departamentos" },
            new() { Chave = "rh-admin", Nome = "Administrador do setor RH", Grupo = "Administradores de setor" },
            new() { Chave = "cartao-ponto", Nome = "Controle de cartão de ponto", Grupo = "Departamentos" },
            new() { Chave = "rh-aprovacao", Nome = "Aprova solicitação do RH", Grupo = "Aprovações" },
            new() { Chave = "gestao-pessoas", Nome = "Gestao de pessoas", Grupo = "Departamentos" },
            new() { Chave = "gestao-pessoas-admin", Nome = "Administrador de gestao de pessoas", Grupo = "Administradores de setor" },
            new() { Chave = "ti", Nome = "Chamados de TI", Grupo = "Departamentos" },
            new() { Chave = "ti-admin", Nome = "Administrador do setor TI", Grupo = "Administradores de setor" },
            new() { Chave = "base-conhecimento-ti", Nome = "Base de conhecimento TI", Grupo = "Departamentos" },
            new() { Chave = "equipamentos-ti", Nome = "Equipamentos de TI", Grupo = "Departamentos" },
            new() { Chave = "compras", Nome = "Compras", Grupo = "Departamentos" },
            new() { Chave = "compras-admin", Nome = "Administrador do setor de Compras", Grupo = "Administradores de setor" },
            new() { Chave = "controladoria", Nome = "Controladoria", Grupo = "Departamentos" },
            new() { Chave = "pecas-admin", Nome = "Administrador do setor de Peças", Grupo = "Administradores de setor" },
            new() { Chave = "vendas-pecas", Nome = "BI de Peças", Grupo = "BI" },
            new() { Chave = "veiculos", Nome = "Estoque de veículos", Grupo = "Veículos" },
            new() { Chave = "veiculos-repasses", Nome = "Repasse de veículos", Grupo = "Veículos" },
            new() { Chave = "veiculos-bi", Nome = "BI de veículos", Grupo = "BI" },
            new() { Chave = "usuarios", Nome = "Cadastro de usuários", Grupo = "Cadastros" },
            new() { Chave = "empresas-revendas", Nome = "Cadastro de empresas e revendas", Grupo = "Cadastros" },
            new() { Chave = "perfis", Nome = "Cadastro de perfis", Grupo = "Cadastros" },
        };

        private static readonly Dictionary<string, string[]> PerfisPadrao = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = AcessosDisponiveis.Select(a => a.Chave).ToArray(),
            ["TI"] = new[]
            {
                "dashboard-admin",
                "ti",
                "ti-admin",
                "base-conhecimento-ti",
                "equipamentos-ti",
                "controladoria",
                "vendas-pecas",
                "veiculos",
                "veiculos-repasses",
                "veiculos-bi",
                "usuarios",
                "empresas-revendas",
                "perfis"
            },
            ["RH"] = new[] { "dashboard-rh", "rh", "rh-admin", "cartao-ponto", "gestao-pessoas", "gestao-pessoas-admin" },
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
            return AcessosDisponiveis
                .Where(acesso => !string.Equals(acesso.Chave, "perfis", StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Grupo)
                .ThenBy(a => a.Nome)
                .ToList();
        }

        public async Task<List<PerfilSistemaDto>> ListAsync(string role, IEnumerable<string>? acessos = null)
        {
            EnsureCanManage(role, acessos);
            await EnsureDefaultsAsync();
            var perfis = await _context.PerfilSistema
                .Include(p => p.Acessos)
                .Include(p => p.Empresas)
                .OrderBy(p => p.Nome)
                .ToListAsync();

            return perfis
                .Select(p => new PerfilSistemaDto
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    PadraoSistema = p.PadraoSistema,
                    Acessos = p.Acessos.Select(a => NormalizeAcessoChave(a.Chave)).Distinct().OrderBy(a => a).ToList(),
                    Empresas = p.Empresas.Select(e => e.EmpresaNumero).OrderBy(e => e).ToList()
                })
                .ToList();
        }

        public async Task<PerfilSistemaDto> SaveAsync(string role, PerfilSistemaSaveDto dto, IEnumerable<string>? userAcessos = null)
        {
            EnsureCanManage(role, userAcessos);
            await EnsureDefaultsAsync();

            var nome = dto.Nome.Trim();
            if (string.IsNullOrWhiteSpace(nome))
                throw new InvalidOperationException("Nome do perfil é obrigatório.");

            var validKeys = AcessosDisponiveis.Select(a => a.Chave).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var acessos = dto.Acessos
                .Select(NormalizeAcessoChave)
                .Where(validKeys.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var empresas = dto.Empresas
                .Where(numero => numero > 0)
                .Distinct()
                .OrderBy(numero => numero)
                .ToList();

            var perfilId = dto.Id.GetValueOrDefault();
            var perfil = perfilId > 0
                ? await _context.PerfilSistema.Include(p => p.Acessos).Include(p => p.Empresas).FirstOrDefaultAsync(p => p.Id == perfilId)
                : await _context.PerfilSistema.Include(p => p.Acessos).Include(p => p.Empresas).FirstOrDefaultAsync(p => p.Nome.ToLower() == nome.ToLower());
            if (perfil is null)
            {
                if (perfilId > 0)
                    throw new InvalidOperationException("Perfil não encontrado.");

                perfil = new PerfilSistema { Nome = nome, PadraoSistema = false };
                _context.PerfilSistema.Add(perfil);
            }
            else
            {
                var duplicateName = await _context.PerfilSistema
                    .AnyAsync(p => p.Id != perfil.Id && p.Nome.ToLower() == nome.ToLower());
                if (duplicateName)
                    throw new InvalidOperationException("Já existe um perfil com este nome.");

                if (perfil.PadraoSistema && !string.Equals(perfil.Nome, nome, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Perfis padrão do sistema não podem ser renomeados.");

                var previousName = perfil.Nome;
                perfil.Nome = nome;
                _context.PerfilSistemaAcesso.RemoveRange(perfil.Acessos);
                _context.PerfilSistemaEmpresa.RemoveRange(perfil.Empresas);

                if (!perfil.PadraoSistema && !string.Equals(previousName, nome, StringComparison.OrdinalIgnoreCase))
                {
                    var users = await _context.User.Where(u => u.Role == previousName).ToListAsync();
                    foreach (var user in users)
                        user.Role = nome;
                }
            }

            foreach (var acesso in acessos)
                perfil.Acessos.Add(new PerfilSistemaAcesso { Chave = acesso });
            foreach (var empresa in empresas)
                perfil.Empresas.Add(new PerfilSistemaEmpresa { EmpresaNumero = empresa });

            await _context.SaveChangesAsync();
            return new PerfilSistemaDto { Id = perfil.Id, Nome = perfil.Nome, PadraoSistema = perfil.PadraoSistema, Acessos = acessos, Empresas = empresas };
        }

        public async Task<List<string>> GetAcessosByPerfilAsync(string role)
        {
            await EnsureDefaultsAsync();
            var normalized = NormalizePerfilName(role);
            var acessos = await _context.PerfilSistema
                .Where(p => p.Nome == normalized)
                .SelectMany(p => p.Acessos.Select(a => a.Chave))
                .ToListAsync();

            return acessos
                .Select(NormalizeAcessoChave)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<int>> GetEmpresasByPerfilAsync(string role)
        {
            await EnsureDefaultsAsync();
            var normalized = NormalizePerfilName(role);
            return await _context.PerfilSistema
                .Where(p => p.Nome == normalized)
                .SelectMany(p => p.Empresas.Select(e => e.EmpresaNumero))
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();
        }

        public async Task DeleteAsync(string role, int id, IEnumerable<string>? acessos = null)
        {
            EnsureCanManage(role, acessos);
            await EnsureDefaultsAsync();

            var perfil = await _context.PerfilSistema.Include(p => p.Acessos).FirstOrDefaultAsync(p => p.Id == id);
            if (perfil is null)
                throw new InvalidOperationException("Perfil não encontrado.");

            if (perfil.PadraoSistema)
                throw new InvalidOperationException("Perfis padrão do sistema não podem ser excluídos.");

            var inUse = await _context.User.AnyAsync(u => u.Role == perfil.Nome);
            if (inUse)
                throw new InvalidOperationException("Este perfil está vinculado a usuários e não pode ser excluído.");

            _context.PerfilSistema.Remove(perfil);
            await _context.SaveChangesAsync();
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

        public static string NormalizeAcessoChave(string chave)
        {
            return LegacyPecasBiAcessos.Contains(chave) ? "vendas-pecas" : chave;
        }

        private static void EnsureCanManage(string role, IEnumerable<string>? acessos = null)
        {
            if (!RoleScope.IsAdmin(role) && !RoleScope.IsTI(role) && !(acessos?.Contains("perfis", StringComparer.OrdinalIgnoreCase) ?? false))
                throw new UnauthorizedAccessException("Somente Admin ou TI podem gerenciar perfis.");
        }
    }
}
