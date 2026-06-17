using UniFlowHub.Api.Data.Interfaces;
using UniFlowHub.Api.Dtos.SolicitacoesRH;
using UniFlowHub.Api.Models;
using UniFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace UniFlowHub.Api.Services
{
    public class SolicitacoesRHService
    {
        private readonly ISolicitacoesRHRepo _repo;
        private readonly IUserRepo _userRepo;

        public SolicitacoesRHService(ISolicitacoesRHRepo repo, IUserRepo userRepo)
        {
            _repo = repo;
            _userRepo = userRepo;
        }

        public List<SolicitacoesRHResponseDto> List(string role, int userId, IEnumerable<string> acessos)
        {
            var query = _repo.Query().AsNoTracking();

            if (!CanManage(role, acessos))
                query = query.Where(s => s.Userid == userId);

            return query
                .OrderByDescending(s => s.DataSolicitacao)
                .Select(s => MapResponse(s))
                .ToList();
        }

        public SolicitacoesRHResponseDto Add(SolicitacoesRHCreateDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            Validate(dto.Unidade, dto.Titulo, dto.Descricao);

            var ownerUserId = CanManage(role, acessos)
                ? (dto.Userid > 0 ? dto.Userid : currentUserId)
                : currentUserId;

            if (!_userRepo.Query().Any(u => u.Id == ownerUserId))
                throw new InvalidOperationException("Usuario solicitante invalido.");

            var solicitacao = new SolicitacoesRH
            {
                Unidade = dto.Unidade.Trim(),
                Titulo = dto.Titulo.Trim(),
                TipoSolicitacao = dto.TipoSolicitacao.Trim(),
                Solicitante = dto.Solicitante.Trim(),
                AnexossUrl = dto.AnexossUrl?.Trim() ?? string.Empty,
                Departamento = dto.Departamento.Trim(),
                Descricao = dto.Descricao.Trim(),
                Prioridade = dto.Prioridade.Trim(),
                DataSolicitacao = DateTime.UtcNow,
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Aberta" : dto.Status.Trim(),
                Responsavel = dto.Responsavel?.Trim() ?? string.Empty,
                Observacoes = dto.Observacoes?.Trim() ?? string.Empty,
                Userid = ownerUserId,
            };

            _repo.Add(solicitacao);
            _repo.Save();

            return MapResponse(solicitacao);
        }

        public SolicitacoesRHResponseDto Update(int id, SolicitacoesRHUpdateDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            var solicitacao = _repo.Query().FirstOrDefault(s => s.Id == id);
            if (solicitacao is null)
                throw new KeyNotFoundException("Solicitacao nao encontrada.");

            if (!CanManage(role, acessos) && solicitacao.Userid != currentUserId)
                throw new UnauthorizedAccessException("Voce nao pode alterar esta solicitacao.");

            if (IsFinalizada(solicitacao))
                throw new InvalidOperationException("Solicitacoes encerradas nao podem ser editadas. Reabra a solicitacao para alterar.");

            Validate(dto.Unidade, dto.Titulo, dto.Descricao);
            var status = string.IsNullOrWhiteSpace(dto.Status) ? solicitacao.Status : dto.Status.Trim();
            if (string.Equals(status, "Concluida", StringComparison.OrdinalIgnoreCase) && !solicitacao.DataEncerramento.HasValue)
                throw new InvalidOperationException("Use o encerramento da solicitacao para informar as observacoes obrigatorias.");

            solicitacao.Unidade = dto.Unidade.Trim();
            solicitacao.Titulo = dto.Titulo.Trim();
            solicitacao.TipoSolicitacao = dto.TipoSolicitacao.Trim();
            solicitacao.Solicitante = dto.Solicitante.Trim();
            solicitacao.Departamento = dto.Departamento.Trim();
            solicitacao.Descricao = dto.Descricao.Trim();
            solicitacao.AnexossUrl = dto.AnexossUrl.Trim();
            solicitacao.Prioridade = dto.Prioridade.Trim();
            solicitacao.Responsavel = dto.Responsavel.Trim();
            solicitacao.Observacoes = dto.Observacoes.Trim();
            solicitacao.Status = status;

            _repo.Update(solicitacao);
            _repo.Save();

            return MapResponse(solicitacao);
        }

        public List<SolicitacaoRHComunicacaoResponseDto> ListComunicacoes(int id, string role, int currentUserId, IEnumerable<string> acessos)
        {
            GetAccessibleSolicitacao(id, role, currentUserId, acessos);

            return _repo.QueryComunicacoes()
                .AsNoTracking()
                .Where(s => s.SolicitacaoRHId == id)
                .OrderBy(s => s.DataCriacao)
                .Select(s => MapComunicacao(s))
                .ToList();
        }

        public SolicitacaoRHComunicacaoResponseDto AddComunicacao(int id, SolicitacaoRHComunicacaoCreateDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            var solicitacao = GetAccessibleSolicitacao(id, role, currentUserId, acessos);
            if (IsFinalizada(solicitacao))
                throw new InvalidOperationException("Solicitacoes encerradas ou canceladas nao permitem novas mensagens.");

            var mensagem = dto.Mensagem?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mensagem))
                throw new InvalidOperationException("Mensagem e obrigatoria.");

            var user = _userRepo.Query().AsNoTracking().FirstOrDefault(u => u.Id == currentUserId);
            if (user is null)
                throw new UnauthorizedAccessException("Usuario invalido.");

            var comunicacao = new SolicitacaoRHComunicacao
            {
                SolicitacaoRHId = id,
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

        public SolicitacoesRHResponseDto Encerrar(int id, SolicitacoesRHEncerrarDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            if (!CanManage(role, acessos))
                throw new UnauthorizedAccessException("Somente administradores de RH podem encerrar solicitacoes.");

            var observacoesEncerramento = dto.ObservacoesEncerramento?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(observacoesEncerramento))
                throw new InvalidOperationException("Observacoes de encerramento sao obrigatorias.");

            var solicitacao = GetAccessibleSolicitacao(id, role, currentUserId, acessos);
            solicitacao.Status = "Concluida";
            solicitacao.DataEncerramento = DateTime.UtcNow;
            solicitacao.ObservacoesEncerramento = observacoesEncerramento;
            solicitacao.SatisfacaoNota = null;
            solicitacao.SatisfacaoComentario = string.Empty;
            solicitacao.DataAvaliacao = null;

            _repo.Update(solicitacao);
            _repo.Save();

            return MapResponse(solicitacao);
        }

        public SolicitacoesRHResponseDto Reabrir(int id, string role, int currentUserId, IEnumerable<string> acessos)
        {
            if (!CanManage(role, acessos))
                throw new UnauthorizedAccessException("Somente administradores de RH podem reabrir solicitacoes.");

            var solicitacao = GetAccessibleSolicitacao(id, role, currentUserId, acessos);
            solicitacao.Status = "Aberta";
            solicitacao.DataEncerramento = null;
            solicitacao.ObservacoesEncerramento = string.Empty;
            solicitacao.SatisfacaoNota = null;
            solicitacao.SatisfacaoComentario = string.Empty;
            solicitacao.DataAvaliacao = null;

            _repo.Update(solicitacao);
            _repo.Save();

            return MapResponse(solicitacao);
        }

        public SolicitacoesRHResponseDto AvaliarSatisfacao(int id, SolicitacoesRHSatisfacaoDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            if (CanManage(role, acessos))
                throw new UnauthorizedAccessException("Administradores apenas visualizam a pesquisa de satisfacao.");

            var solicitacao = GetAccessibleSolicitacao(id, role, currentUserId, acessos);
            if (!solicitacao.DataEncerramento.HasValue)
                throw new InvalidOperationException("A pesquisa de satisfacao so pode ser preenchida apos o encerramento da solicitacao.");

            if (solicitacao.SatisfacaoNota.HasValue)
                throw new InvalidOperationException("Esta solicitacao ja foi avaliada.");

            if (dto.Nota < 1 || dto.Nota > 5)
                throw new InvalidOperationException("A nota da satisfacao deve ser entre 1 e 5.");

            solicitacao.SatisfacaoNota = dto.Nota;
            solicitacao.SatisfacaoComentario = dto.Comentario?.Trim() ?? string.Empty;
            solicitacao.DataAvaliacao = DateTime.UtcNow;

            _repo.Update(solicitacao);
            _repo.Save();

            return MapResponse(solicitacao);
        }

        public SolicitacoesRH GetAttachmentOwner(int id, string role, int currentUserId, IEnumerable<string> acessos)
        {
            var solicitacao = _repo.Query().AsNoTracking().FirstOrDefault(s => s.Id == id);
            if (solicitacao is null)
                throw new KeyNotFoundException("Solicitacao nao encontrada.");

            if (!CanManage(role, acessos) && solicitacao.Userid != currentUserId)
                throw new UnauthorizedAccessException("Voce nao pode acessar este anexo.");

            if (string.IsNullOrWhiteSpace(solicitacao.AnexossUrl))
                throw new FileNotFoundException("Esta solicitacao nao possui anexo.");

            return solicitacao;
        }

        private static void Validate(string unidade, string titulo, string descricao)
        {
            if (string.IsNullOrWhiteSpace(unidade))
                throw new InvalidOperationException("Unidade e obrigatoria.");

            if (string.IsNullOrWhiteSpace(titulo))
                throw new InvalidOperationException("Titulo e obrigatorio.");

            if (string.IsNullOrWhiteSpace(descricao))
                throw new InvalidOperationException("Descricao e obrigatoria.");
        }

        private static bool CanManage(string role, IEnumerable<string> acessos)
        {
            return RoleScope.IsAdmin(role) || HasAccess(acessos, "rh-admin");
        }

        private static bool HasAccess(IEnumerable<string> acessos, string chave)
            => acessos.Any(acesso => string.Equals(acesso, chave, StringComparison.OrdinalIgnoreCase));

        private static bool IsFinalizada(SolicitacoesRH solicitacao)
        {
            return solicitacao.DataEncerramento.HasValue
                || string.Equals(solicitacao.Status, "Concluida", StringComparison.OrdinalIgnoreCase)
                || string.Equals(solicitacao.Status, "Cancelada", StringComparison.OrdinalIgnoreCase);
        }

        private SolicitacoesRH GetAccessibleSolicitacao(int id, string role, int currentUserId, IEnumerable<string> acessos)
        {
            var solicitacao = _repo.Query().FirstOrDefault(s => s.Id == id);
            if (solicitacao is null)
                throw new KeyNotFoundException("Solicitacao nao encontrada.");

            if (!CanManage(role, acessos) && solicitacao.Userid != currentUserId)
                throw new UnauthorizedAccessException("Voce nao pode acessar esta solicitacao.");

            return solicitacao;
        }

        private static SolicitacoesRHResponseDto MapResponse(SolicitacoesRH s)
        {
            return new SolicitacoesRHResponseDto
            {
                Id = s.Id,
                Userid = s.Userid,
                Unidade = s.Unidade,
                Titulo = s.Titulo,
                TipoSolicitacao = s.TipoSolicitacao,
                Solicitante = s.Solicitante,
                Departamento = s.Departamento,
                Descricao = s.Descricao,
                Responsavel = s.Responsavel,
                Prioridade = s.Prioridade,
                AnexossUrl = s.AnexossUrl,
                DataSolicitacao = s.DataSolicitacao,
                DataEncerramento = s.DataEncerramento,
                Status = s.Status,
                Observacoes = s.Observacoes,
                ObservacoesEncerramento = s.ObservacoesEncerramento,
                SatisfacaoNota = s.SatisfacaoNota,
                SatisfacaoComentario = s.SatisfacaoComentario,
                DataAvaliacao = s.DataAvaliacao,
                AvaliacaoPendente = s.DataEncerramento.HasValue && !s.SatisfacaoNota.HasValue
            };
        }

        private static SolicitacaoRHComunicacaoResponseDto MapComunicacao(SolicitacaoRHComunicacao s)
        {
            return new SolicitacaoRHComunicacaoResponseDto
            {
                Id = s.Id,
                SolicitacaoRHId = s.SolicitacaoRHId,
                Mensagem = s.Mensagem,
                AutorNome = s.AutorNome,
                AutorRole = s.AutorRole,
                AutorUserId = s.AutorUserId,
                DataCriacao = s.DataCriacao
            };
        }
    }
}
