using UniFlowHub.Api.Data;
using UniFlowHub.Api.Data.Interfaces;
using UniFlowHub.Api.Dtos;
using UniFlowHub.Api.Models;
using UniFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace UniFlowHub.Api.Services
{
    public class UsersService
    {
        private readonly IUserRepo _repo;
        private readonly AppDbContext _context;

        public UsersService(IUserRepo repo, AppDbContext context)
        {
            _repo = repo;
            _context = context;
        }

        private static List<UserResponseDto> MapUsers(IQueryable<Users> query)
        {
            return query.Select(u => new UserResponseDto
            {
                Id = u.Id,
                Nome = u.Nome,
                Cpf = u.Cpf,
                Email = u.Email,
                Role = u.Role,
                Perfis = u.Perfis.Select(p => p.Perfil).ToList(),
                Departamento = u.Departamento,
                Cargo = u.Cargo,
                Ativo = u.Ativo,
                UnidadeId = u.UnidadeId,
                UnidadeNome = u.Unidade != null ? u.Unidade.Nome : string.Empty,
                DataNascimento = u.DataNascimento
            }).ToList();
        }

        public List<UserResponseDto> List(string role, string email, IEnumerable<string>? acessos = null)
        {
            IQueryable<Users> user = _repo.Query().Include(u => u.Unidade).Include(u => u.Perfis);

            if (CanViewUsersDirectory(role, acessos))
                return MapUsers(user);

            user = user.Where(u => u.Email == email);
            return MapUsers(user);
        }

        public List<UserResponseDto> ListAdministradores(string role, IEnumerable<string>? acessos = null)
        {
            if (!CanManageUsers(role, acessos))
                throw new UnauthorizedAccessException("Voce nao pode listar administradores.");

            return MapUsers(_repo.Query()
                .Include(u => u.Unidade)
                .Include(u => u.Perfis)
                .Where(u => u.Role == "Admin" || u.Role == "TI")
                .OrderBy(u => u.Nome));
        }

        public UserResponseDto Update(int id, UserUpdateDto dto, string role, IEnumerable<string>? currentUserAcessos = null)
        {
            if (!CanManageUsers(role, currentUserAcessos))
                throw new UnauthorizedAccessException("Voce nao pode editar usuarios.");

            var user = _repo.Query().Include(u => u.Unidade).Include(u => u.Perfis).FirstOrDefault(u => u.Id == id);
            if (user is null)
                throw new KeyNotFoundException("Usuario nao encontrado.");

            var email = dto.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(dto.Nome))
                throw new InvalidOperationException("Nome e obrigatorio.");

            if (string.IsNullOrWhiteSpace(dto.Cpf))
                throw new InvalidOperationException("CPF e obrigatorio.");

            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("Email e obrigatorio.");

            dto.Role = AuthService.NormalizeRole(dto.Role);
            var perfis = AuthService.NormalizePerfilList(dto.Role, dto.Perfis);
            EnsureConfiguredRoles(perfis);
            dto.UnidadeId = NormalizeUnidadeId(dto.UnidadeId);

            AuthService.ValidateUnidadeForRole(dto.Role, dto.UnidadeId);
            ValidateUnidadeExists(dto.UnidadeId);

            if (_repo.Query().Any(u => u.Id != id && u.Email == email))
                throw new InvalidOperationException("Ja existe um usuario com este email.");

            user.Nome = dto.Nome.Trim();
            user.Cpf = dto.Cpf.Trim();
            user.Email = email;
            user.Role = dto.Role;
            user.Departamento = dto.Departamento.Trim();
            user.Cargo = dto.Cargo.Trim();
            user.Ativo = dto.Ativo;
            user.UnidadeId = dto.UnidadeId;
            user.DataNascimento = dto.DataNascimento;
            SaveUserPerfis(user, perfis);

            if (!string.IsNullOrWhiteSpace(dto.Senha))
            {
                if (dto.Senha.Length < 6)
                    throw new InvalidOperationException("Senha deve ter pelo menos 6 caracteres.");

                user.Senha = PasswordHasher.Hash(dto.Senha);
            }

            _repo.Update(user);
            _repo.Save();

            return MapUsers(_repo.Query().Include(u => u.Unidade).Include(u => u.Perfis).Where(u => u.Id == id)).Single();
        }

        private void EnsureConfiguredRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                throw new InvalidOperationException("Perfil invalido.");

            if (!_context.PerfilSistema.Any(p => p.Nome == role))
                throw new InvalidOperationException("Perfil invalido.");
        }

        private void EnsureConfiguredRoles(IEnumerable<string> perfis)
        {
            var configured = _context.PerfilSistema.Select(p => p.Nome).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!perfis.Any() || perfis.Any(perfil => !configured.Contains(perfil)))
                throw new InvalidOperationException("Perfil invalido.");
        }

        private static bool CanManageUsers(string role, IEnumerable<string>? acessos)
        {
            return RoleScope.IsAdmin(role)
                || RoleScope.IsTI(role)
                || (acessos?.Contains("usuarios", StringComparer.OrdinalIgnoreCase) ?? false);
        }

        private static bool CanViewUsersDirectory(string role, IEnumerable<string>? acessos)
        {
            return CanManageUsers(role, acessos)
                || RoleScope.IsRH(role)
                || (acessos?.Contains("rh-admin", StringComparer.OrdinalIgnoreCase) ?? false)
                || (acessos?.Contains("dashboard-rh", StringComparer.OrdinalIgnoreCase) ?? false);
        }

        private void SaveUserPerfis(Users user, List<string> perfis)
        {
            _context.UserPerfil.RemoveRange(user.Perfis);
            user.Perfis.Clear();
            foreach (var perfil in perfis)
                user.Perfis.Add(new UserPerfil { Perfil = perfil });
        }

        private static int? NormalizeUnidadeId(int? unidadeId)
            => unidadeId.HasValue && unidadeId.Value > 0 ? unidadeId : null;

        private void ValidateUnidadeExists(int? unidadeId)
        {
            if (unidadeId.HasValue && !_context.Unidade.Any(unidade => unidade.Id == unidadeId.Value))
                throw new InvalidOperationException("Empresa e revenda informadas nao foram encontradas.");
        }

        public UserResponseDto UpdateProfile(int id, UserProfileUpdateDto dto)
        {
            var user = _repo.Query().Include(u => u.Unidade).FirstOrDefault(u => u.Id == id);
            if (user is null)
                throw new KeyNotFoundException("Usuario nao encontrado.");

            if (string.IsNullOrWhiteSpace(dto.Nome))
                throw new InvalidOperationException("Nome e obrigatorio.");

            if (string.IsNullOrWhiteSpace(dto.Cpf))
                throw new InvalidOperationException("CPF e obrigatorio.");

            user.Nome = dto.Nome.Trim();
            user.Cpf = dto.Cpf.Trim();
            user.Departamento = dto.Departamento.Trim();
            user.Cargo = dto.Cargo.Trim();
            user.DataNascimento = dto.DataNascimento;

            _repo.Update(user);
            _repo.Save();

            return MapUsers(_repo.Query().Include(u => u.Unidade).Where(u => u.Id == id)).Single();
        }

        public void ChangePassword(int id, UserChangePasswordDto dto)
        {
            var user = _repo.Query().FirstOrDefault(u => u.Id == id);
            if (user is null)
                throw new KeyNotFoundException("Usuario nao encontrado.");

            if (!PasswordHasher.Verify(dto.SenhaAtual, user.Senha))
                throw new UnauthorizedAccessException("Senha atual invalida.");

            if (string.IsNullOrWhiteSpace(dto.NovaSenha) || dto.NovaSenha.Length < 6)
                throw new InvalidOperationException("Nova senha deve ter pelo menos 6 caracteres.");

            user.Senha = PasswordHasher.Hash(dto.NovaSenha);
            _repo.Update(user);
            _repo.Save();
        }

    }
}
