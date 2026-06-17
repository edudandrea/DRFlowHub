using UniFlowHub.Api.Data.Interfaces;
using UniFlowHub.Api.Dtos.SolicitacoesCompra;
using UniFlowHub.Api.Models;
using UniFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace UniFlowHub.Api.Services
{
    public class SolicitacoesCompraService
    {
        private readonly ISolicitacoesCompraRepo _repo;
        private readonly IUserRepo _userRepo;

        public SolicitacoesCompraService(ISolicitacoesCompraRepo repo, IUserRepo userRepo)
        {
            _repo = repo;
            _userRepo = userRepo;
        }

        public List<SolicitacaoCompraResponseDto> List(string role, int userId, IEnumerable<string> acessos)
        {
            var query = _repo.Query().AsNoTracking();

            if (!CanViewAll(role, acessos))
                query = query.Where(s => s.Userid == userId);

            return query
                .OrderByDescending(s => s.DataSolicitacao)
                .Select(s => MapResponse(s))
                .ToList();
        }

        public SolicitacaoCompraResponseDto Add(SolicitacaoCompraCreateDto dto, string role, int currentUserId, string documentoUrl, IEnumerable<string> acessos)
        {
            Validate(dto.Titulo, dto.Descricao, dto.Justificativa, dto.FornecedorSugerido);

            var ownerUserId = CanViewAll(role, acessos)
                ? (dto.Userid > 0 ? dto.Userid : currentUserId)
                : currentUserId;

            if (!_userRepo.Query().Any(u => u.Id == ownerUserId))
                throw new InvalidOperationException("Usuario solicitante invalido.");

            var solicitacao = new SolicitacaoCompra
            {
                Titulo = dto.Titulo.Trim(),
                Categoria = string.IsNullOrWhiteSpace(dto.Categoria) ? "Material" : dto.Categoria.Trim(),
                Descricao = dto.Descricao.Trim(),
                Solicitante = dto.Solicitante.Trim(),
                Unidade = dto.Unidade.Trim(),
                Departamento = dto.Departamento.Trim(),
                ValorEstimado = dto.ValorEstimado ?? 0,
                FornecedorSugerido = dto.FornecedorSugerido.Trim(),
                Prioridade = string.IsNullOrWhiteSpace(dto.Prioridade) ? "Media" : dto.Prioridade.Trim(),
                Status = "Aguardando Diretoria",
                Justificativa = dto.Justificativa.Trim(),
                Observacoes = dto.Observacoes?.Trim() ?? string.Empty,
                DocumentoUrl = documentoUrl,
                DataSolicitacao = DateTime.UtcNow,
                Userid = ownerUserId
            };

            _repo.Add(solicitacao);
            _repo.Save();

            return MapResponse(solicitacao);
        }

        public SolicitacaoCompraResponseDto Update(int id, SolicitacaoCompraUpdateDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            var solicitacao = GetAccessibleSolicitacao(id, role, currentUserId, acessos);

            if (!CanManageCompras(role, acessos))
                throw new UnauthorizedAccessException("Somente Compras pode atualizar a etapa de compra.");

            if (IsFinalizada(solicitacao))
                throw new InvalidOperationException("Solicitacoes concluidas nao podem ser editadas.");

            Validate(dto.Titulo, dto.Descricao, dto.Justificativa, dto.FornecedorSugerido);

            solicitacao.Titulo = dto.Titulo.Trim();
            solicitacao.Categoria = dto.Categoria.Trim();
            solicitacao.Descricao = dto.Descricao.Trim();
            solicitacao.Solicitante = dto.Solicitante.Trim();
            solicitacao.Unidade = dto.Unidade.Trim();
            solicitacao.Departamento = dto.Departamento.Trim();
            solicitacao.ValorEstimado = dto.ValorEstimado;
            solicitacao.FornecedorSugerido = dto.FornecedorSugerido.Trim();
            solicitacao.Prioridade = dto.Prioridade.Trim();
            solicitacao.Justificativa = dto.Justificativa.Trim();
            solicitacao.Observacoes = dto.Observacoes.Trim();
            solicitacao.Comprador = dto.Comprador.Trim();
            solicitacao.ObservacoesCompras = dto.ObservacoesCompras.Trim();

            var status = string.IsNullOrWhiteSpace(dto.Status) ? solicitacao.Status : dto.Status.Trim();
            solicitacao.Status = status;
            if (string.Equals(status, "Em compras", StringComparison.OrdinalIgnoreCase) && !solicitacao.DataEnvioCompras.HasValue)
                solicitacao.DataEnvioCompras = DateTime.UtcNow;
            if (string.Equals(status, "Concluida", StringComparison.OrdinalIgnoreCase))
                solicitacao.DataConclusao = DateTime.UtcNow;

            _repo.Update(solicitacao);
            _repo.Save();

            return MapResponse(solicitacao);
        }

        public List<SolicitacaoCompraComunicacaoResponseDto> ListComunicacoes(int id, string role, int currentUserId, IEnumerable<string> acessos)
        {
            GetAccessibleSolicitacao(id, role, currentUserId, acessos, asNoTracking: true);

            return _repo.QueryComunicacoes()
                .AsNoTracking()
                .Where(s => s.SolicitacaoCompraId == id)
                .OrderBy(s => s.DataCriacao)
                .Select(s => MapComunicacao(s))
                .ToList();
        }

        public SolicitacaoCompraComunicacaoResponseDto AddComunicacao(int id, SolicitacaoCompraComunicacaoCreateDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            var solicitacao = GetAccessibleSolicitacao(id, role, currentUserId, acessos);
            if (IsFinalizada(solicitacao))
                throw new InvalidOperationException("Solicitacoes concluidas ou canceladas nao permitem novas mensagens.");

            var mensagem = dto.Mensagem?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mensagem))
                throw new InvalidOperationException("Mensagem e obrigatoria.");

            var user = _userRepo.Query().AsNoTracking().FirstOrDefault(u => u.Id == currentUserId);
            if (user is null)
                throw new UnauthorizedAccessException("Usuario invalido.");

            var comunicacao = new SolicitacaoCompraComunicacao
            {
                SolicitacaoCompraId = id,
                Mensagem = mensagem,
                AutorNome = user.Nome,
                AutorRole = role,
                AutorUserId = currentUserId,
                DataCriacao = DateTime.UtcNow
            };

            _repo.AddComunicacao(comunicacao);
            _repo.Save();

            return MapComunicacao(comunicacao);
        }

        public SolicitacaoCompraResponseDto Aprovar(int id, SolicitacaoCompraAprovacaoDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            if (!CanApprove(role))
                throw new UnauthorizedAccessException("Somente Diretoria pode aprovar solicitacoes de compras.");

            var solicitacao = GetAccessibleSolicitacao(id, role, currentUserId, acessos);
            var aprovador = _userRepo.Query().AsNoTracking().FirstOrDefault(u => u.Id == currentUserId);

            solicitacao.Status = dto.Aprovada ? "Aprovada - Enviada para Compras" : "Reprovada";
            solicitacao.DataAprovacao = DateTime.UtcNow;
            solicitacao.DataEnvioCompras = dto.Aprovada ? DateTime.UtcNow : null;
            solicitacao.Aprovador = aprovador?.Nome ?? "Diretoria";
            solicitacao.ObservacoesAprovacao = dto.ObservacoesAprovacao.Trim();

            _repo.Update(solicitacao);
            _repo.Save();

            return MapResponse(solicitacao);
        }

        public SolicitacaoCompra GetAttachmentOwner(int id, string role, int currentUserId, IEnumerable<string> acessos)
        {
            var solicitacao = GetAccessibleSolicitacao(id, role, currentUserId, acessos, asNoTracking: true);
            if (string.IsNullOrWhiteSpace(solicitacao.DocumentoUrl))
                throw new FileNotFoundException("Esta solicitacao nao possui documento.");

            return solicitacao;
        }

        private static bool CanViewAll(string role, IEnumerable<string> acessos)
        {
            return RoleScope.IsAdmin(role) || RoleScope.IsDiretoria(role) || HasAccess(acessos, "compras-admin");
        }

        private static bool CanApprove(string role)
        {
            return RoleScope.IsAdmin(role) || RoleScope.IsDiretoria(role);
        }

        private static bool CanManageCompras(string role, IEnumerable<string> acessos)
        {
            return RoleScope.IsAdmin(role) || HasAccess(acessos, "compras-admin");
        }

        private static bool HasAccess(IEnumerable<string> acessos, string chave)
            => acessos.Any(acesso => string.Equals(acesso, chave, StringComparison.OrdinalIgnoreCase));

        private static bool IsFinalizada(SolicitacaoCompra solicitacao)
        {
            return solicitacao.DataConclusao.HasValue
                || string.Equals(solicitacao.Status, "Concluida", StringComparison.OrdinalIgnoreCase)
                || string.Equals(solicitacao.Status, "Cancelada", StringComparison.OrdinalIgnoreCase)
                || string.Equals(solicitacao.Status, "Reprovada", StringComparison.OrdinalIgnoreCase);
        }

        private SolicitacaoCompra GetAccessibleSolicitacao(int id, string role, int currentUserId, IEnumerable<string> acessos, bool asNoTracking = false)
        {
            var query = asNoTracking ? _repo.Query().AsNoTracking() : _repo.Query();
            var solicitacao = query.FirstOrDefault(s => s.Id == id);
            if (solicitacao is null)
                throw new KeyNotFoundException("Solicitacao de compra nao encontrada.");

            if (!CanViewAll(role, acessos) && solicitacao.Userid != currentUserId)
                throw new UnauthorizedAccessException("Voce nao pode acessar esta solicitacao de compra.");

            return solicitacao;
        }

        private static void Validate(string titulo, string descricao, string justificativa, string fornecedorSugerido)
        {
            if (string.IsNullOrWhiteSpace(titulo))
                throw new InvalidOperationException("Titulo e obrigatorio.");

            if (string.IsNullOrWhiteSpace(descricao))
                throw new InvalidOperationException("Descricao e obrigatoria.");

            if (string.IsNullOrWhiteSpace(justificativa))
                throw new InvalidOperationException("Justificativa e obrigatoria.");

            if (string.IsNullOrWhiteSpace(fornecedorSugerido))
                throw new InvalidOperationException("Fornecedor sugerido e obrigatorio.");
        }

        private static SolicitacaoCompraResponseDto MapResponse(SolicitacaoCompra s)
        {
            return new SolicitacaoCompraResponseDto
            {
                Id = s.Id,
                Titulo = s.Titulo,
                Categoria = s.Categoria,
                Descricao = s.Descricao,
                Solicitante = s.Solicitante,
                Unidade = s.Unidade,
                Departamento = s.Departamento,
                ValorEstimado = s.ValorEstimado,
                FornecedorSugerido = s.FornecedorSugerido,
                Prioridade = s.Prioridade,
                Status = s.Status,
                Justificativa = s.Justificativa,
                Observacoes = s.Observacoes,
                DocumentoUrl = s.DocumentoUrl,
                DataSolicitacao = s.DataSolicitacao,
                DataAprovacao = s.DataAprovacao,
                DataEnvioCompras = s.DataEnvioCompras,
                DataConclusao = s.DataConclusao,
                Aprovador = s.Aprovador,
                Comprador = s.Comprador,
                ObservacoesAprovacao = s.ObservacoesAprovacao,
                ObservacoesCompras = s.ObservacoesCompras,
                Userid = s.Userid
            };
        }

        private static SolicitacaoCompraComunicacaoResponseDto MapComunicacao(SolicitacaoCompraComunicacao s)
        {
            return new SolicitacaoCompraComunicacaoResponseDto
            {
                Id = s.Id,
                SolicitacaoCompraId = s.SolicitacaoCompraId,
                Mensagem = s.Mensagem,
                AutorNome = s.AutorNome,
                AutorRole = s.AutorRole,
                AutorUserId = s.AutorUserId,
                DataCriacao = s.DataCriacao
            };
        }
    }
}
