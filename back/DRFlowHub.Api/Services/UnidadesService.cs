using DRFlowHub.Api.Data;
using DRFlowHub.Api.Data.Interfaces;
using DRFlowHub.Api.Dtos.Unidades;
using DRFlowHub.Api.Models;
using DRFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace DRFlowHub.Api.Services
{
    public class UnidadesService
    {
        private readonly IUnidadesRepo _repo;
        private readonly AppDbContext _context;

        public UnidadesService(IUnidadesRepo repo, AppDbContext context)
        {
            _repo = repo;
            _context = context;
        }

        public List<UnidadeResponseDto> List(string role)
        {
            IQueryable<Unidade> query = _repo.Query()
                .AsNoTracking()
                .Include(u => u.EmpresaCadastro);

            if (RoleScope.IsQualidadeNissan(role))
                query = query.Where(u => u.EmpresaCadastro != null && u.EmpresaCadastro.Numero == 2);

            return query
                .OrderBy(u => u.EmpresaCadastro == null ? u.Empresa : u.EmpresaCadastro.Nome)
                .ThenBy(u => u.NumeroRevenda)
                .ThenBy(u => u.Nome)
                .Select(u => MapResponse(u))
                .ToList();
        }

        public List<EmpresaResponseDto> ListEmpresas(string role)
        {
            var query = _context.Empresa
                .AsNoTracking()
                .AsQueryable();

            if (RoleScope.IsQualidadeNissan(role))
                query = query.Where(e => e.Numero == 2);

            return query
                .OrderBy(e => e.Numero)
                .ThenBy(e => e.Nome)
                .Select(e => MapEmpresa(e))
                .ToList();
        }

        public EmpresaResponseDto AddEmpresa(EmpresaCreateDto dto, string role)
        {
            EnsureCanManage(role);
            ValidateEmpresa(dto);

            if (_context.Empresa.Any(e => e.Numero == dto.Numero))
                throw new InvalidOperationException("Ja existe uma empresa com este numero.");

            var empresa = new Empresa
            {
                Numero = dto.Numero,
                Nome = dto.Nome.Trim(),
                DataCadastro = DateTime.UtcNow
            };

            _context.Empresa.Add(empresa);
            _context.SaveChanges();

            return MapEmpresa(empresa);
        }

        public EmpresaResponseDto UpdateEmpresa(int id, EmpresaCreateDto dto, string role)
        {
            EnsureCanManage(role);
            ValidateEmpresa(dto);

            var empresa = _context.Empresa.FirstOrDefault(e => e.Id == id);
            if (empresa is null)
                throw new KeyNotFoundException("Empresa nao encontrada.");

            if (_context.Empresa.Any(e => e.Id != id && e.Numero == dto.Numero))
                throw new InvalidOperationException("Ja existe uma empresa com este numero.");

            empresa.Numero = dto.Numero;
            empresa.Nome = dto.Nome.Trim();

            var revendas = _repo.Query().Where(u => u.EmpresaId == id).ToList();
            foreach (var revenda in revendas)
            {
                revenda.Empresa = empresa.Nome;
                revenda.Nome = BuildNome(empresa.Nome, revenda.Revenda);
            }

            _context.SaveChanges();

            return MapEmpresa(empresa);
        }

        public UnidadeResponseDto Add(UnidadeCreateDto dto, string role)
        {
            EnsureCanManage(role);
            Validate(dto);

            var empresaEntity = GetEmpresa(dto.EmpresaId);
            var empresa = empresaEntity.Nome;
            var revenda = dto.Revenda.Trim();
            var cnpj = NormalizeCnpj(dto.Cnpj);
            if (_repo.Query().Any(u => u.EmpresaId == empresaEntity.Id && u.NumeroRevenda == dto.NumeroRevenda))
                throw new InvalidOperationException("Ja existe uma revenda com este numero para esta empresa.");

            var unidade = new Unidade
            {
                Nome = BuildNome(empresa, revenda),
                EmpresaId = empresaEntity.Id,
                EmpresaCadastro = empresaEntity,
                NumeroRevenda = dto.NumeroRevenda,
                Empresa = empresa,
                Revenda = revenda,
                Cnpj = cnpj,
                Endereco = dto.Endereco.Trim(),
                DataCadastro = DateTime.UtcNow
            };

            _repo.Add(unidade);
            _repo.Save();

            return MapResponse(unidade);
        }

        public UnidadeResponseDto Update(int id, UnidadeCreateDto dto, string role)
        {
            EnsureCanManage(role);
            Validate(dto);

            var unidade = _repo.Query().FirstOrDefault(u => u.Id == id);
            if (unidade is null)
                throw new KeyNotFoundException("Unidade nao encontrada.");

            var empresaEntity = GetEmpresa(dto.EmpresaId);
            var empresa = empresaEntity.Nome;
            var revenda = dto.Revenda.Trim();
            var cnpj = NormalizeCnpj(dto.Cnpj);
            if (_repo.Query().Any(u => u.Id != id && u.EmpresaId == empresaEntity.Id && u.NumeroRevenda == dto.NumeroRevenda))
                throw new InvalidOperationException("Ja existe uma revenda com este numero para esta empresa.");

            unidade.Nome = BuildNome(empresa, revenda);
            unidade.EmpresaId = empresaEntity.Id;
            unidade.EmpresaCadastro = empresaEntity;
            unidade.NumeroRevenda = dto.NumeroRevenda;
            unidade.Empresa = empresa;
            unidade.Revenda = revenda;
            unidade.Cnpj = cnpj;
            unidade.Endereco = dto.Endereco.Trim();

            _repo.Update(unidade);
            _repo.Save();

            return MapResponse(unidade);
        }

        private static void EnsureCanManage(string role)
        {
            if (!RoleScope.IsAdmin(role) && !RoleScope.IsTI(role))
                throw new UnauthorizedAccessException("Voce nao pode administrar unidades.");
        }

        private static void Validate(UnidadeCreateDto dto)
        {
            if (dto.EmpresaId <= 0)
                throw new InvalidOperationException("Empresa e obrigatoria.");

            if (dto.NumeroRevenda <= 0)
                throw new InvalidOperationException("Numero da revenda e obrigatorio.");

            if (string.IsNullOrWhiteSpace(dto.Revenda))
                throw new InvalidOperationException("Revenda e obrigatoria.");

            if (string.IsNullOrWhiteSpace(dto.Cnpj))
                throw new InvalidOperationException("CNPJ e obrigatorio.");

            if (string.IsNullOrWhiteSpace(dto.Endereco))
                throw new InvalidOperationException("Endereco e obrigatorio.");
        }

        private static string NormalizeCnpj(string cnpj)
        {
            return new string(cnpj.Where(char.IsDigit).ToArray());
        }

        private static string BuildNome(string empresa, string revenda)
        {
            return $"{empresa.Trim()} - {revenda.Trim()}";
        }

        private Empresa GetEmpresa(int id)
        {
            var empresa = _context.Empresa.FirstOrDefault(e => e.Id == id);
            return empresa ?? throw new InvalidOperationException("Empresa nao encontrada.");
        }

        private static void ValidateEmpresa(EmpresaCreateDto dto)
        {
            if (dto.Numero <= 0)
                throw new InvalidOperationException("Numero da empresa e obrigatorio.");

            if (string.IsNullOrWhiteSpace(dto.Nome))
                throw new InvalidOperationException("Nome da empresa e obrigatorio.");
        }

        private static UnidadeResponseDto MapResponse(Unidade unidade)
        {
            var empresa = unidade.EmpresaCadastro?.Nome ?? (string.IsNullOrWhiteSpace(unidade.Empresa) ? unidade.Nome : unidade.Empresa);
            var revenda = string.IsNullOrWhiteSpace(unidade.Revenda) ? unidade.Nome : unidade.Revenda;

            return new UnidadeResponseDto
            {
                Id = unidade.Id,
                Nome = string.IsNullOrWhiteSpace(unidade.Nome) ? BuildNome(empresa, revenda) : unidade.Nome,
                EmpresaId = unidade.EmpresaId,
                EmpresaNumero = unidade.EmpresaCadastro?.Numero ?? 0,
                NumeroRevenda = unidade.NumeroRevenda,
                Empresa = empresa,
                Revenda = revenda,
                Cnpj = unidade.Cnpj,
                Endereco = unidade.Endereco,
                DataCadastro = unidade.DataCadastro
            };
        }

        private static EmpresaResponseDto MapEmpresa(Empresa empresa)
        {
            return new EmpresaResponseDto
            {
                Id = empresa.Id,
                Numero = empresa.Numero,
                Nome = empresa.Nome,
                DataCadastro = empresa.DataCadastro
            };
        }
    }
}
