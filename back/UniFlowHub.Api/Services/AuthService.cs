using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UniFlowHub.Api.Data;
using UniFlowHub.Api.Data.Interfaces;
using UniFlowHub.Api.Dtos;
using UniFlowHub.Api.Dtos.Auth;
using UniFlowHub.Api.Models;
using UniFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace UniFlowHub.Api.Services
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
            dto.Role = NormalizeRole(string.IsNullOrWhiteSpace(dto.Role) ? dto.Cargo : dto.Role);
            var perfis = new List<string>();
            dto.UnidadeId = NormalizeUnidadeId(dto.UnidadeId);

            ValidateUser(dto.Nome, dto.Cpf, dto.Email, dto.Senha, dto.Role);
            ValidateUnidadeForRole(dto.Role, dto.UnidadeId);
            ValidateUnidadeExists(dto.UnidadeId);
            ValidateCargoExists(dto.Cargo);

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
                UnidadeId = dto.UnidadeId,
                DataNascimento = dto.DataNascimento,
                CreatedByUserId = createdByUserId
            };

            _repo.Add(user);
            _repo.Save();

            return ToResponse(_repo.Query().Include(u => u.Unidade).First(u => u.Id == user.Id), BuildAcessos(user), perfis);
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
            var perfis = new List<string>();
            var acessos = BuildAcessos(user);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Nome),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, role)
            };
            claims.AddRange(perfis.Select(perfil => new Claim("perfil", perfil)));
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
                User = ToResponse(user, acessos, perfis)
            };
        }

        private List<string> BuildAcessos(Users user)
        {
            if (RoleScope.IsAdmin(user.Role))
                return PerfisService.AcessosDisponiveis
                    .Select(a => PerfisService.NormalizeAcessoChave(a.Chave))
                    .Where(chave => !string.Equals(chave, "perfis", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(acesso => acesso)
                    .ToList();

            var cargoKeys = new[] { user.Cargo, user.Role }
                .Select(NormalizeText)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var acessos = _context.GestaoPessoasCargo
                .AsNoTracking()
                .Include(cargo => cargo.Acessos)
                .Where(cargo => cargo.Ativo)
                .AsEnumerable()
                .Where(cargo => cargoKeys.Contains(NormalizeText(cargo.Nome)))
                .SelectMany(cargo => cargo.Acessos.Select(acesso => acesso.Chave))
                .ToList();

            acessos.AddRange(GetDefaultAcessosForUser(user));

            return acessos
                .Select(PerfisService.NormalizeAcessoChave)
                .Where(chave => !string.Equals(chave, "perfis", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(acesso => acesso)
                .ToList();
        }

        private static List<string> GetDefaultAcessosForUser(Users user)
        {
            var acessos = new HashSet<string>(GetDefaultAcessos(user.Role, user.Departamento, Array.Empty<string>()), StringComparer.OrdinalIgnoreCase);
            var normalizedRole = NormalizeText(user.Role);
            var normalizedCargo = NormalizeText(user.Cargo);
            var normalizedDepartamento = NormalizeText(user.Departamento);
            var isTi = RoleScope.IsTI(user.Role)
                || IsTiDepartment(normalizedRole)
                || IsTiDepartment(normalizedCargo)
                || IsTiDepartment(normalizedDepartamento);
            var isRh = RoleScope.IsRH(user.Role)
                || IsRhDepartment(normalizedRole)
                || IsRhDepartment(normalizedCargo)
                || IsRhDepartment(normalizedDepartamento);

            if (isTi)
            {
                acessos.Add("ti-admin");
                acessos.Add("usuarios");
                acessos.Add("empresas-revendas");
                acessos.Add("base-conhecimento-ti");
                acessos.Add("equipamentos-ti");
            }

            if (isRh)
            {
                acessos.Add("rh-admin");
                acessos.Add("gestao-pessoas");
                acessos.Add("gestao-pessoas-admin");
                acessos.Add("cartao-ponto");
            }

            return acessos.ToList();
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

        private static int? NormalizeUnidadeId(int? unidadeId)
            => unidadeId.HasValue && unidadeId.Value > 0 ? unidadeId : null;

        private void ValidateUnidadeExists(int? unidadeId)
        {
            if (unidadeId.HasValue && !_context.Unidade.Any(unidade => unidade.Id == unidadeId.Value))
                throw new InvalidOperationException("Empresa e revenda informadas nao foram encontradas.");
        }

        public static UserResponseDto ToResponse(Users user, List<string>? acessos = null, List<string>? perfis = null)
        {
            var userPerfis = perfis ?? new List<string>();
            return new UserResponseDto
            {
                Id = user.Id,
                Nome = user.Nome,
                Cpf = user.Cpf,
                Email = user.Email,
                Role = NormalizeRole(user.Role),
                Perfis = userPerfis,
                Departamento = user.Departamento,
                Cargo = user.Cargo,
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

        private void ValidateCargoExists(string cargo)
        {
            if (string.IsNullOrWhiteSpace(cargo))
                throw new InvalidOperationException("Cargo e obrigatorio.");

            if (!_repo.HasAnyUser())
                return;

            if (!_context.GestaoPessoasCargo.Any(c => c.Ativo && c.Nome == cargo.Trim()))
                throw new InvalidOperationException("Cargo invalido.");
        }

        private void ValidateConfiguredRoles(IEnumerable<string> perfis)
        {
            EnsureDefaultPerfis();
            var configured = _context.PerfilSistema.Select(p => p.Nome).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!perfis.Any() || perfis.Any(perfil => !configured.Contains(perfil)))
                throw new InvalidOperationException("Perfil invalido.");
        }

        public static List<string> NormalizePerfilList(string role, IEnumerable<string> perfis)
        {
            var result = new List<string>();
            var primary = NormalizeRole(role);
            if (!string.IsNullOrWhiteSpace(primary))
                result.Add(primary);

            result.AddRange(perfis
                .Select(NormalizeRole)
                .Where(perfil => !string.IsNullOrWhiteSpace(perfil)));

            return result
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<string> GetDefaultAcessos(string role, string departamento, IEnumerable<string> perfis)
        {
            var acessos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ti",
                "rh",
                "compras"
            };

            var normalizedRole = NormalizeText(role);
            var normalizedDepartamento = NormalizeText(departamento);
            var normalizedPerfis = perfis.Select(NormalizeText).ToList();
            var isTi = normalizedRole == "ti"
                || IsTiDepartment(normalizedDepartamento)
                || normalizedPerfis.Any(perfil => perfil == "ti" || perfil == "t.i" || perfil == "tecnologia");
            var isRh = normalizedRole == "rh"
                || IsRhDepartment(normalizedDepartamento)
                || normalizedPerfis.Any(perfil => perfil == "rh" || perfil == "recursos humanos");
            var isCompras = normalizedRole == "compras"
                || IsComprasDepartment(normalizedDepartamento)
                || normalizedPerfis.Any(perfil => perfil == "compras");

            if (isTi)
            {
                acessos.Add("ti-admin");
                acessos.Add("usuarios");
                acessos.Add("empresas-revendas");
                acessos.Add("base-conhecimento-ti");
                acessos.Add("equipamentos-ti");
                acessos.Add("perfis");
            }

            if (isRh)
            {
                acessos.Add("rh-admin");
                acessos.Add("gestao-pessoas");
                acessos.Add("gestao-pessoas-admin");
                acessos.Add("cartao-ponto");
            }

            if (isCompras)
            {
                acessos.Add("compras-admin");
            }

            return acessos.ToList();
        }

        private List<string> GetUserPerfis(int userId, string role)
        {
            var perfis = _context.UserPerfil
                .Where(p => p.UserId == userId)
                .Select(p => p.Perfil)
                .ToList();

            return NormalizePerfilList(role, perfis);
        }

        private void SaveUserPerfis(int userId, List<string> perfis)
        {
            var existing = _context.UserPerfil.Where(p => p.UserId == userId).ToList();
            _context.UserPerfil.RemoveRange(existing);
            foreach (var perfil in perfis)
                _context.UserPerfil.Add(new UserPerfil { UserId = userId, Perfil = perfil });
            _context.SaveChanges();
        }

        private static string NormalizeText(string? value)
        {
            var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (var ch in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    builder.Append(ch);
            }
            return builder.ToString().ToLowerInvariant().Trim();
        }

        private static bool IsTiDepartment(string normalizedDepartamento)
        {
            if (string.IsNullOrWhiteSpace(normalizedDepartamento))
                return false;

            return normalizedDepartamento == "ti"
                || normalizedDepartamento == "t.i"
                || normalizedDepartamento.Contains("tecnologia")
                || normalizedDepartamento.Split(new[] { ' ', '-', '_', '/', '\\', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(token => token == "ti" || token == "t.i");
        }

        private static bool IsRhDepartment(string normalizedDepartamento)
        {
            if (string.IsNullOrWhiteSpace(normalizedDepartamento))
                return false;

            return normalizedDepartamento == "rh"
                || normalizedDepartamento.Contains("recursos humanos")
                || normalizedDepartamento.Split(new[] { ' ', '-', '_', '/', '\\', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(token => token == "rh");
        }

        private static bool IsComprasDepartment(string normalizedDepartamento)
        {
            if (string.IsNullOrWhiteSpace(normalizedDepartamento))
                return false;

            return normalizedDepartamento == "compras"
                || normalizedDepartamento.Contains("compras")
                || normalizedDepartamento.Split(new[] { ' ', '-', '_', '/', '\\', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(token => token == "compras");
        }

        private void EnsureDefaultPerfis()
        {
            var defaults = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Admin"] = PerfisService.AcessosDisponiveis.Select(a => a.Chave).ToArray(),
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
