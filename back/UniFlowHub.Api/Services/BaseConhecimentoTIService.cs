using UniFlowHub.Api.Data;
using UniFlowHub.Api.Dtos.BaseConhecimentoTI;
using UniFlowHub.Api.Models;
using UniFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace UniFlowHub.Api.Services
{
    public class BaseConhecimentoTIService
    {
        private readonly AppDbContext _context;

        public BaseConhecimentoTIService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<BaseConhecimentoTIResponseDto>> ListAsync(string role, IEnumerable<string> acessos)
        {
            EnsureCanAccess(role, acessos);

            return await _context.BaseConhecimentoTI
                .AsNoTracking()
                .Include(item => item.OwnerUser)
                .OrderByDescending(item => item.DataAtualizacao ?? item.DataCadastro)
                .ThenBy(item => item.Titulo)
                .Select(item => MapResponse(item))
                .ToListAsync();
        }

        public async Task<BaseConhecimentoTIResponseDto> AddAsync(
            BaseConhecimentoTICreateDto dto,
            string role,
            int userId,
            IEnumerable<string> acessos,
            string arquivoUrl,
            string arquivoNome,
            string arquivoContentType)
        {
            EnsureCanAccess(role, acessos);
            Validate(dto.Titulo, dto.Descricao);

            var item = new BaseConhecimentoTI
            {
                Titulo = dto.Titulo.Trim(),
                Categoria = string.IsNullOrWhiteSpace(dto.Categoria) ? "Geral" : dto.Categoria.Trim(),
                Descricao = dto.Descricao.Trim(),
                Tags = dto.Tags?.Trim() ?? string.Empty,
                ArquivoNome = arquivoNome,
                ArquivoUrl = arquivoUrl,
                ArquivoContentType = arquivoContentType,
                DataCadastro = DateTime.UtcNow,
                Userid = userId
            };

            _context.BaseConhecimentoTI.Add(item);
            await _context.SaveChangesAsync();

            return await GetResponseAsync(item.Id);
        }

        public async Task<BaseConhecimentoTIResponseDto> UpdateAsync(
            int id,
            BaseConhecimentoTIUpdateDto dto,
            string role,
            IEnumerable<string> acessos,
            string? arquivoUrl,
            string? arquivoNome,
            string? arquivoContentType)
        {
            EnsureCanAccess(role, acessos);
            Validate(dto.Titulo, dto.Descricao);

            var item = await _context.BaseConhecimentoTI.FirstOrDefaultAsync(item => item.Id == id);
            if (item is null)
                throw new KeyNotFoundException("Conhecimento nao encontrado.");

            item.Titulo = dto.Titulo.Trim();
            item.Categoria = string.IsNullOrWhiteSpace(dto.Categoria) ? "Geral" : dto.Categoria.Trim();
            item.Descricao = dto.Descricao.Trim();
            item.Tags = dto.Tags?.Trim() ?? string.Empty;
            item.DataAtualizacao = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(arquivoUrl))
            {
                item.ArquivoUrl = arquivoUrl;
                item.ArquivoNome = arquivoNome ?? string.Empty;
                item.ArquivoContentType = arquivoContentType ?? string.Empty;
            }

            await _context.SaveChangesAsync();
            return await GetResponseAsync(item.Id);
        }

        public async Task DeleteAsync(int id, string role, IEnumerable<string> acessos)
        {
            EnsureCanAccess(role, acessos);

            var item = await _context.BaseConhecimentoTI.FirstOrDefaultAsync(item => item.Id == id);
            if (item is null)
                throw new KeyNotFoundException("Conhecimento nao encontrado.");

            _context.BaseConhecimentoTI.Remove(item);
            await _context.SaveChangesAsync();
        }

        public async Task<BaseConhecimentoTI> GetAttachmentOwnerAsync(int id, string role, IEnumerable<string> acessos)
        {
            EnsureCanAccess(role, acessos);

            var item = await _context.BaseConhecimentoTI.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
            if (item is null)
                throw new KeyNotFoundException("Conhecimento nao encontrado.");

            if (string.IsNullOrWhiteSpace(item.ArquivoUrl))
                throw new FileNotFoundException("Este conhecimento nao possui anexo.");

            return item;
        }

        private async Task<BaseConhecimentoTIResponseDto> GetResponseAsync(int id)
        {
            var item = await _context.BaseConhecimentoTI
                .AsNoTracking()
                .Include(item => item.OwnerUser)
                .FirstAsync(item => item.Id == id);

            return MapResponse(item);
        }

        private static void EnsureCanAccess(string role, IEnumerable<string> acessos)
        {
            if (RoleScope.IsAdmin(role) || RoleScope.IsTI(role) || HasAccess(acessos, "base-conhecimento-ti"))
                return;

            throw new UnauthorizedAccessException("Somente usuarios da TI podem acessar a base de conhecimento.");
        }

        private static bool HasAccess(IEnumerable<string> acessos, string chave)
            => acessos.Any(acesso => string.Equals(acesso, chave, StringComparison.OrdinalIgnoreCase));

        private static void Validate(string titulo, string descricao)
        {
            if (string.IsNullOrWhiteSpace(titulo))
                throw new InvalidOperationException("Titulo e obrigatorio.");

            if (string.IsNullOrWhiteSpace(descricao))
                throw new InvalidOperationException("Descricao e obrigatoria.");
        }

        private static BaseConhecimentoTIResponseDto MapResponse(BaseConhecimentoTI item)
        {
            return new BaseConhecimentoTIResponseDto
            {
                Id = item.Id,
                Titulo = item.Titulo,
                Categoria = item.Categoria,
                Descricao = item.Descricao,
                Tags = item.Tags,
                ArquivoNome = item.ArquivoNome,
                ArquivoUrl = item.ArquivoUrl,
                ArquivoContentType = item.ArquivoContentType,
                DataCadastro = item.DataCadastro,
                DataAtualizacao = item.DataAtualizacao,
                Userid = item.Userid,
                AutorNome = item.OwnerUser?.Nome ?? string.Empty
            };
        }
    }
}
