using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DRFlowHub.Api.Data;
using DRFlowHub.Api.Data.Interfaces;
using DRFlowHub.Api.Dtos;
using DRFlowHub.Api.Dtos.Auth;
using DRFlowHub.Api.Models;
using DRFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DRFlowHub.Api.Services
{
    public class AuthService
    {
        private readonly IUserRepo _repo;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;

        public AuthService(IUserRepo repo, IConfiguration configuration, AppDbContext context)
        {
            _repo = repo;
            _configuration = configuration;
            _context = context;
        }

        public LoginResponseDto Login(LoginRequestDto dto)
        {
            var user = _repo.GetByLogin(dto.Email.Trim().ToLowerInvariant());

            if (user is null || !PasswordHasher.Verify(dto.Senha, user.Senha))
                throw new UnauthorizedAccessException("Email ou senha invalidos.");

            if (!user.Ativo)
                throw new UnauthorizedAccessException("Usuario inativo. Contate o administrador.");

            return CreateLoginResponse(user);
        }

        public UserResponseDto CreateUser(UserCreateDto dto, int? createdByUserId)
        {
            dto.Role = NormalizeRole(dto.Role);
            if (!ShouldRequireUnidade(dto.Role))
                dto.UnidadeId = null;

            ValidateUser(dto.Nome, dto.Cpf, dto.Email, dto.Senha, dto.Role);
            ValidateConfiguredRole(dto.Role);
            ValidateUnidadeForRole(dto.Role, dto.UnidadeId);

            if (_repo.Query().Any(u => u.Email == dto.Email.Trim()))
                throw new InvalidOperationException("Ja existe um usuario com este email.");

            if (_repo.Query().Any(u => u.Cpf == dto.Cpf.Trim()))
                throw new InvalidOperationException("Ja existe um usuario com este CPF.");

            var user = new Users
            {
                Nome = dto.Nome.Trim(),
                Cpf = dto.Cpf.Trim(),
                Email = dto.Email.Trim().ToLowerInvariant(),
                Senha = PasswordHasher.Hash(dto.Senha),
                Role = dto.Role,
                Departamento = dto.Departamento.Trim(),
                Cargo = dto.Cargo.Trim(),
                Ativo = dto.Ativo,
                UnidadeId = ShouldRequireUnidade(dto.Role) ? dto.UnidadeId : null,
                DataNascimento = dto.DataNascimento,
                CreatedByUserId = createdByUserId
            };

            _repo.Add(user);
            _repo.Save();

            return ToResponse(_repo.Query().Include(u => u.Unidade).First(u => u.Id == user.Id));
        }

        public bool HasAnyUser()
        {
            return _repo.HasAnyUser();
        }

        private LoginResponseDto CreateLoginResponse(Users user)
        {
            EnsureDefaultPerfis();
            var expiresAt = DateTime.UtcNow.AddMinutes(
                _configuration.GetValue<int?>("Jwt:ExpiresMinutes") ?? 480);

            var role = NormalizeRole(user.Role);
            var acessos = _context.PerfilSistema
                .Where(p => p.Nome == role)
                .SelectMany(p => p.Acessos.Select(a => a.Chave))
                .Distinct()
                .ToList();

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Nome),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, role)
            };
            claims.AddRange(acessos.Select(acesso => new Claim("access", acesso)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return new LoginResponseDto
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAt = expiresAt,
                User = ToResponse(user, acessos)
            };
        }

        private static void ValidateUser(string nome, string cpf, string email, string senha, string role)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new InvalidOperationException("Nome e obrigatorio.");

            if (string.IsNullOrWhiteSpace(cpf))
                throw new InvalidOperationException("CPF e obrigatorio.");

            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("Email e obrigatorio.");

            if (string.IsNullOrWhiteSpace(senha) || senha.Length < 6)
                throw new InvalidOperationException("Senha deve ter pelo menos 6 caracteres.");

            if (string.IsNullOrWhiteSpace(role))
                throw new InvalidOperationException("Perfil invalido.");
        }

        public static bool IsValidRole(string role)
        {
            return RoleScope.IsAdmin(role)
                || RoleScope.IsRH(role)
                || RoleScope.IsTI(role)
                || RoleScope.IsDiretoria(role)
                || RoleScope.IsCompras(role)
                || RoleScope.IsControladoria(role)
                || RoleScope.IsQualidadeNissan(role)
                || RoleScope.IsGerenteGeralPecas(role)
                || RoleScope.IsGerentePecas(role)
                || RoleScope.IsVendedorPecas(role)
                || RoleScope.IsGerente(role)
                || RoleScope.IsUser(role);
        }

        public static string NormalizeRole(string role)
        {
            var builtIn = NormalizeBuiltInRole(role);
            return string.IsNullOrWhiteSpace(builtIn) ? role.Trim() : builtIn;
        }

        public static string NormalizeBuiltInRole(string role)
        {
            if (RoleScope.IsAdmin(role)) return "Admin";
            if (RoleScope.IsRH(role)) return "RH";
            if (RoleScope.IsTI(role)) return "TI";
            if (RoleScope.IsDiretoria(role)) return "Diretoria";
            if (RoleScope.IsCompras(role)) return "Compras";
            if (RoleScope.IsControladoria(role)) return "Controladoria";
            if (RoleScope.IsQualidadeNissan(role)) return "Qualidade Nissan";
            if (RoleScope.IsGerenteGeralPecas(role)) return "Gerente Geral de Pecas";
            if (RoleScope.IsGerentePecas(role)) return "Gerente de Pecas";
            if (RoleScope.IsVendedorPecas(role)) return "Vendedor de Pecas";
            if (RoleScope.IsGerente(role)) return "Gestor";
            if (RoleScope.IsUser(role)) return "Usuario";
            return string.Empty;
        }

        public static bool ShouldRequireUnidade(string role)
        {
            if (RoleScope.IsAdmin(role)
                || RoleScope.IsTI(role)
                || RoleScope.IsControladoria(role)
                || RoleScope.IsQualidadeNissan(role)
                || RoleScope.IsGerenteGeralPecas(role)
                || RoleScope.IsVendedorPecas(role)
                || RoleScope.IsRH(role)
                || RoleScope.IsDiretoria(role)
                || RoleScope.IsCompras(role))
            {
                return false;
            }

            return RoleScope.IsGerentePecas(role) || RoleScope.IsGerente(role) || RoleScope.IsUser(role);
        }

        public static void ValidateUnidadeForRole(string role, int? unidadeId)
        {
            if (ShouldRequireUnidade(role) && (!unidadeId.HasValue || unidadeId.Value <= 0))
                throw new InvalidOperationException("Empresa e revenda sao obrigatorias para este perfil.");
        }

        public static UserResponseDto ToResponse(Users user, List<string>? acessos = null)
        {
            return new UserResponseDto
            {
                Id = user.Id,
                Nome = user.Nome,
                Cpf = user.Cpf,
                Email = user.Email,
                Role = NormalizeRole(user.Role),
                Departamento = user.Departamento,
                Cargo = user.Cargo,
                RustDeskId = user.RustDeskId,
                RustDeskHostname = user.RustDeskHostname,
                RustDeskSistemaOperacional = user.RustDeskSistemaOperacional,
                Ativo = user.Ativo,
                UnidadeId = user.UnidadeId,
                UnidadeNome = user.Unidade?.Nome ?? string.Empty,
                DataNascimento = user.DataNascimento,
                Acessos = acessos ?? new List<string>()
            };
        }

        private void ValidateConfiguredRole(string role)
        {
            EnsureDefaultPerfis();
            if (!_context.PerfilSistema.Any(p => p.Nome == role))
                throw new InvalidOperationException("Perfil invalido.");
        }

        private void EnsureDefaultPerfis()
        {
            var defaults = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Admin"] = PerfisService.AcessosDisponiveis.Select(a => a.Chave).ToArray(),
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

            foreach (var item in defaults)
            {
                var perfil = _context.PerfilSistema.Include(p => p.Acessos).FirstOrDefault(p => p.Nome == item.Key);
                if (perfil is null)
                {
                    perfil = new PerfilSistema { Nome = item.Key, PadraoSistema = true };
                    foreach (var acesso in item.Value)
                        perfil.Acessos.Add(new PerfilSistemaAcesso { Chave = acesso });
                    _context.PerfilSistema.Add(perfil);
                    continue;
                }

                if (!perfil.PadraoSistema)
                    continue;

                var current = perfil.Acessos.Select(a => a.Chave).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var acesso in item.Value.Where(acesso => !current.Contains(acesso)))
                    perfil.Acessos.Add(new PerfilSistemaAcesso { Chave = acesso });
            }

            _context.SaveChanges();
        }
    }
}
