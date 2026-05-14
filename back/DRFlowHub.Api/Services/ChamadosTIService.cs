using DRFlowHub.Api.Data.Interfaces;
using DRFlowHub.Api.Dtos.ChamadosTI;
using DRFlowHub.Api.Models;
using DRFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace DRFlowHub.Api.Services
{
    public class ChamadosTIService
    {
        private readonly IChamadosTIRepo _repo;
        private readonly IUserRepo _userRepo;
        private readonly IConfiguration _configuration;

        public ChamadosTIService(IChamadosTIRepo repo, IUserRepo userRepo, IConfiguration configuration)
        {
            _repo = repo;
            _userRepo = userRepo;
            _configuration = configuration;
        }

        public List<ChamadoTIResponseDto> List(string role, int userId, IEnumerable<string> acessos)
        {
            IQueryable<ChamadosTI> query = _repo.Query()
                .AsNoTracking()
                .Include(s => s.Comunicacoes);

            if (!CanManage(role, acessos))
                query = query.Where(s => s.Userid == userId);

            return query
                .OrderByDescending(s => s.DataAbertura)
                .Select(s => MapResponse(s))
                .ToList();
        }

        public ChamadoTIResponseDto Add(ChamadoTICreateDto dto, string role, int currentUserId, string imagemUrl, IEnumerable<string> acessos)
        {
            var titulo = dto.Titulo?.Trim() ?? string.Empty;
            var descricao = dto.Descricao?.Trim() ?? string.Empty;
            Validate(titulo, descricao);

            var ownerUserId = CanManage(role, acessos)
                ? (dto.Userid > 0 ? dto.Userid : currentUserId)
                : currentUserId;

            var ownerUser = _userRepo.Query().AsNoTracking().FirstOrDefault(u => u.Id == ownerUserId);
            if (ownerUser is null)
                throw new InvalidOperationException("Usuario solicitante invalido.");

            var chamado = new ChamadosTI
            {
                Titulo = titulo,
                Categoria = string.IsNullOrWhiteSpace(dto.Categoria) ? "Suporte" : dto.Categoria.Trim(),
                Descricao = descricao,
                Solicitante = dto.Solicitante?.Trim() ?? string.Empty,
                Unidade = dto.Unidade?.Trim() ?? string.Empty,
                Departamento = dto.Departamento?.Trim() ?? string.Empty,
                Prioridade = string.IsNullOrWhiteSpace(dto.Prioridade) ? "Media" : dto.Prioridade.Trim(),
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Aberto" : dto.Status.Trim(),
                Responsavel = dto.Responsavel?.Trim() ?? string.Empty,
                AcessoRemotoUrl = dto.AcessoRemotoUrl?.Trim() ?? string.Empty,
                RustDeskId = string.IsNullOrWhiteSpace(dto.RustDeskId) ? ownerUser.RustDeskId : dto.RustDeskId.Trim(),
                RustDeskSenha = string.IsNullOrWhiteSpace(dto.RustDeskSenha) ? ownerUser.RustDeskSenha : dto.RustDeskSenha.Trim(),
                RustDeskServidor = GetRustDeskServer(dto.RustDeskServidor),
                RustDeskKey = GetRustDeskKey(dto.RustDeskKey),
                EquipamentoNome = string.IsNullOrWhiteSpace(dto.EquipamentoNome) || dto.EquipamentoNome == "Nao informado pelo navegador"
                    ? ownerUser.RustDeskHostname
                    : dto.EquipamentoNome.Trim(),
                EquipamentoIp = dto.EquipamentoIp?.Trim() ?? string.Empty,
                EquipamentoSistemaOperacional = string.IsNullOrWhiteSpace(dto.EquipamentoSistemaOperacional)
                    ? ownerUser.RustDeskSistemaOperacional
                    : dto.EquipamentoSistemaOperacional.Trim(),
                Observacoes = dto.Observacoes?.Trim() ?? string.Empty,
                AnexoImagemUrl = imagemUrl,
                DataAbertura = DateTime.UtcNow,
                Userid = ownerUserId,
            };

            _repo.Add(chamado);
            _repo.Save();

            return MapResponse(chamado);
        }

        public ChamadoTIResponseDto Update(int id, ChamadoTIUpdateDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            var chamado = GetAccessibleChamado(id, role, currentUserId, acessos);

            if (IsFinalizado(chamado))
                throw new InvalidOperationException("Chamados encerrados nao podem ser editados. Reabra o chamado para alterar.");

            var titulo = dto.Titulo?.Trim() ?? string.Empty;
            var descricao = dto.Descricao?.Trim() ?? string.Empty;
            Validate(titulo, descricao);

            chamado.Titulo = titulo;
            chamado.Categoria = dto.Categoria?.Trim() ?? string.Empty;
            chamado.Descricao = descricao;
            chamado.Solicitante = dto.Solicitante?.Trim() ?? string.Empty;
            chamado.Unidade = dto.Unidade?.Trim() ?? string.Empty;
            chamado.Departamento = dto.Departamento?.Trim() ?? string.Empty;
            chamado.Prioridade = dto.Prioridade?.Trim() ?? string.Empty;
            chamado.Status = string.IsNullOrWhiteSpace(dto.Status) ? chamado.Status : dto.Status.Trim();
            chamado.Responsavel = dto.Responsavel?.Trim() ?? string.Empty;
            chamado.AcessoRemotoUrl = dto.AcessoRemotoUrl?.Trim() ?? string.Empty;
            chamado.RustDeskId = dto.RustDeskId?.Trim() ?? string.Empty;
            chamado.RustDeskSenha = dto.RustDeskSenha?.Trim() ?? string.Empty;
            chamado.RustDeskServidor = GetRustDeskServer(dto.RustDeskServidor);
            chamado.RustDeskKey = GetRustDeskKey(dto.RustDeskKey);
            chamado.EquipamentoNome = dto.EquipamentoNome?.Trim() ?? chamado.EquipamentoNome;
            chamado.EquipamentoIp = dto.EquipamentoIp?.Trim() ?? chamado.EquipamentoIp;
            chamado.EquipamentoSistemaOperacional = dto.EquipamentoSistemaOperacional?.Trim() ?? chamado.EquipamentoSistemaOperacional;
            chamado.Observacoes = dto.Observacoes?.Trim() ?? string.Empty;

            _repo.Update(chamado);
            _repo.Save();

            return MapResponse(chamado);
        }

        public List<ChamadoTIComunicacaoResponseDto> ListComunicacoes(int id, string role, int currentUserId, IEnumerable<string> acessos)
        {
            GetAccessibleChamado(id, role, currentUserId, acessos, asNoTracking: true);

            return _repo.QueryComunicacoes()
                .AsNoTracking()
                .Where(s => s.ChamadoTIId == id)
                .OrderBy(s => s.DataCriacao)
                .Select(s => MapComunicacao(s))
                .ToList();
        }

        public ChamadoTIComunicacaoResponseDto AddComunicacao(int id, ChamadoTIComunicacaoCreateDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            var chamado = GetAccessibleChamado(id, role, currentUserId, acessos);
            if (IsFinalizado(chamado))
                throw new InvalidOperationException("Chamados encerrados ou cancelados nao permitem novas mensagens.");

            var mensagem = dto.Mensagem?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mensagem))
                throw new InvalidOperationException("Mensagem e obrigatoria.");

            var user = _userRepo.Query().AsNoTracking().FirstOrDefault(u => u.Id == currentUserId);
            if (user is null)
                throw new UnauthorizedAccessException("Usuario invalido.");

            var comunicacao = new ChamadoTIComunicacao
            {
                ChamadoTIId = id,
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

        public ChamadoTIResponseDto Encerrar(int id, ChamadoTIEncerrarDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            if (!CanManage(role, acessos))
                throw new UnauthorizedAccessException("Somente administradores de TI podem encerrar chamados.");

            var observacoesEncerramento = dto.ObservacoesEncerramento?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(observacoesEncerramento))
                throw new InvalidOperationException("Observacoes de encerramento sao obrigatorias.");

            var chamado = GetAccessibleChamado(id, role, currentUserId, acessos);
            if (chamado.DataEncerramento.HasValue && !chamado.DataPrimeiroEncerramento.HasValue)
                chamado.DataPrimeiroEncerramento = chamado.DataEncerramento.Value;

            chamado.Status = "Concluido";
            chamado.DataEncerramento = DateTime.UtcNow;
            chamado.ObservacoesEncerramento = observacoesEncerramento;
            chamado.Reaberto = false;
            chamado.SatisfacaoNota = null;
            chamado.SatisfacaoComentario = string.Empty;
            chamado.DataAvaliacao = null;

            _repo.Update(chamado);
            _repo.Save();

            return MapResponse(chamado);
        }

        public ChamadoTIResponseDto AvaliarSatisfacao(int id, ChamadoTISatisfacaoDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            if (CanManage(role, acessos))
                throw new UnauthorizedAccessException("Administradores apenas visualizam a pesquisa de satisfacao.");

            var chamado = GetAccessibleChamado(id, role, currentUserId, acessos);
            if (!chamado.DataEncerramento.HasValue)
                throw new InvalidOperationException("A pesquisa de satisfacao so pode ser preenchida apos o encerramento do chamado.");

            if (chamado.SatisfacaoNota.HasValue)
                throw new InvalidOperationException("Este chamado ja foi avaliado.");

            if (dto.Nota < 1 || dto.Nota > 5)
                throw new InvalidOperationException("A nota da satisfacao deve ser entre 1 e 5.");

            chamado.SatisfacaoNota = dto.Nota;
            chamado.SatisfacaoComentario = dto.Comentario?.Trim() ?? string.Empty;
            chamado.DataAvaliacao = DateTime.UtcNow;

            _repo.Update(chamado);
            _repo.Save();

            return MapResponse(chamado);
        }

        public ChamadoTIResponseDto Reabrir(int id, string role, int currentUserId, IEnumerable<string> acessos)
        {
            var chamado = GetAccessibleChamado(id, role, currentUserId, acessos);
            if (!IsFinalizado(chamado))
                throw new InvalidOperationException("Somente chamados encerrados ou cancelados podem ser reabertos.");

            if (chamado.DataEncerramento.HasValue && !chamado.DataPrimeiroEncerramento.HasValue)
                chamado.DataPrimeiroEncerramento = chamado.DataEncerramento.Value;

            chamado.Status = "Aberto";
            chamado.DataEncerramento = null;
            chamado.DataReabertura = DateTime.UtcNow;
            chamado.Reaberto = true;
            chamado.SatisfacaoNota = null;
            chamado.SatisfacaoComentario = string.Empty;
            chamado.DataAvaliacao = null;

            _repo.Update(chamado);
            _repo.Save();

            return MapResponse(chamado);
        }

        public ChamadosTI GetAttachmentOwner(int id, string role, int currentUserId, IEnumerable<string> acessos)
        {
            var chamado = _repo.Query().AsNoTracking().FirstOrDefault(s => s.Id == id);
            if (chamado is null)
                throw new KeyNotFoundException("Chamado nao encontrado.");

            if (!CanManage(role, acessos) && chamado.Userid != currentUserId)
                throw new UnauthorizedAccessException("Voce nao pode acessar esta imagem.");

            if (string.IsNullOrWhiteSpace(chamado.AnexoImagemUrl))
                throw new FileNotFoundException("Este chamado nao possui imagem.");

            return chamado;
        }

        public static bool CanManage(string role, IEnumerable<string> acessos)
        {
            return RoleScope.IsAdmin(role) || HasAccess(acessos, "ti-admin");
        }

        private static bool HasAccess(IEnumerable<string> acessos, string chave)
            => acessos.Any(acesso => string.Equals(acesso, chave, StringComparison.OrdinalIgnoreCase));

        private static bool IsFinalizado(ChamadosTI chamado)
        {
            return chamado.DataEncerramento.HasValue
                || string.Equals(chamado.Status, "Concluido", StringComparison.OrdinalIgnoreCase)
                || string.Equals(chamado.Status, "Cancelado", StringComparison.OrdinalIgnoreCase);
        }

        private static void Validate(string titulo, string descricao)
        {
            if (string.IsNullOrWhiteSpace(titulo))
                throw new InvalidOperationException("Titulo e obrigatorio.");

            if (string.IsNullOrWhiteSpace(descricao))
                throw new InvalidOperationException("Descricao e obrigatoria.");
        }

        private static ChamadoTIResponseDto MapResponse(ChamadosTI s)
        {
            return new ChamadoTIResponseDto
            {
                Id = s.Id,
                Userid = s.Userid,
                Titulo = s.Titulo,
                Categoria = s.Categoria,
                Descricao = s.Descricao,
                Solicitante = s.Solicitante,
                Unidade = s.Unidade,
                Departamento = s.Departamento,
                Prioridade = s.Prioridade,
                Status = s.Status,
                Responsavel = s.Responsavel,
                AcessoRemotoUrl = s.AcessoRemotoUrl,
                RustDeskId = s.RustDeskId,
                RustDeskSenha = s.RustDeskSenha,
                RustDeskServidor = s.RustDeskServidor,
                RustDeskKey = s.RustDeskKey,
                EquipamentoNome = s.EquipamentoNome,
                EquipamentoIp = s.EquipamentoIp,
                EquipamentoSistemaOperacional = s.EquipamentoSistemaOperacional,
                AnexoImagemUrl = s.AnexoImagemUrl,
                Observacoes = s.Observacoes,
                ObservacoesEncerramento = s.ObservacoesEncerramento,
                SatisfacaoNota = s.SatisfacaoNota,
                SatisfacaoComentario = s.SatisfacaoComentario,
                DataAvaliacao = s.DataAvaliacao,
                AvaliacaoPendente = s.DataEncerramento.HasValue && !s.SatisfacaoNota.HasValue,
                DataAbertura = s.DataAbertura,
                DataPrimeiroEncerramento = s.DataPrimeiroEncerramento,
                DataReabertura = s.DataReabertura,
                DataEncerramento = s.DataEncerramento,
                UltimaMovimentacao = GetUltimaMovimentacao(s),
                Reaberto = s.Reaberto
            };
        }

        private string GetRustDeskServer(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? _configuration["RustDesk:Server"] ?? string.Empty
                : value.Trim();
        }

        private string GetRustDeskKey(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? _configuration["RustDesk:Key"] ?? string.Empty
                : value.Trim();
        }

        private static DateTime GetUltimaMovimentacao(ChamadosTI chamado)
        {
            var ultima = chamado.DataAbertura;

            if (chamado.DataReabertura.HasValue && chamado.DataReabertura.Value > ultima)
                ultima = chamado.DataReabertura.Value;

            if (chamado.DataEncerramento.HasValue && chamado.DataEncerramento.Value > ultima)
                ultima = chamado.DataEncerramento.Value;

            if (chamado.DataAvaliacao.HasValue && chamado.DataAvaliacao.Value > ultima)
                ultima = chamado.DataAvaliacao.Value;

            var ultimaComunicacao = chamado.Comunicacoes
                .OrderByDescending(c => c.DataCriacao)
                .Select(c => (DateTime?)c.DataCriacao)
                .FirstOrDefault();

            if (ultimaComunicacao.HasValue && ultimaComunicacao.Value > ultima)
                ultima = ultimaComunicacao.Value;

            return ultima;
        }

        private ChamadosTI GetAccessibleChamado(int id, string role, int currentUserId, IEnumerable<string> acessos, bool asNoTracking = false)
        {
            var query = asNoTracking ? _repo.Query().AsNoTracking() : _repo.Query();
            var chamado = query.FirstOrDefault(s => s.Id == id);
            if (chamado is null)
                throw new KeyNotFoundException("Chamado nao encontrado.");

            if (!CanManage(role, acessos) && chamado.Userid != currentUserId)
                throw new UnauthorizedAccessException("Voce nao pode acessar este chamado.");

            return chamado;
        }

        private static ChamadoTIComunicacaoResponseDto MapComunicacao(ChamadoTIComunicacao s)
        {
            return new ChamadoTIComunicacaoResponseDto
            {
                Id = s.Id,
                ChamadoTIId = s.ChamadoTIId,
                Mensagem = s.Mensagem,
                AutorNome = s.AutorNome,
                AutorRole = s.AutorRole,
                AutorUserId = s.AutorUserId,
                DataCriacao = s.DataCriacao
            };
        }
    }
}
