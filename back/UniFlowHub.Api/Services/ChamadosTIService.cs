using UniFlowHub.Api.Data.Interfaces;
using UniFlowHub.Api.Dtos.ChamadosTI;
using UniFlowHub.Api.Models;
using UniFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace UniFlowHub.Api.Services
{
    public class ChamadosTIService
    {
        private readonly IChamadosTIRepo _repo;
        private readonly IUserRepo _userRepo;

        public ChamadosTIService(IChamadosTIRepo repo, IUserRepo userRepo)
        {
            _repo = repo;
            _userRepo = userRepo;
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
                AcessoRemotoSenha = dto.AcessoRemotoSenha?.Trim() ?? string.Empty,
                EquipamentoNome = string.IsNullOrWhiteSpace(dto.EquipamentoNome) || dto.EquipamentoNome == "Nao informado pelo navegador"
                    ? string.Empty
                    : dto.EquipamentoNome.Trim(),
                EquipamentoIp = dto.EquipamentoIp?.Trim() ?? string.Empty,
                EquipamentoSistemaOperacional = dto.EquipamentoSistemaOperacional?.Trim() ?? string.Empty,
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
            chamado.AcessoRemotoSenha = dto.AcessoRemotoSenha?.Trim() ?? string.Empty;
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

        public void EnsureCanAccess(int id, string role, int currentUserId, IEnumerable<string> acessos)
        {
            GetAccessibleChamado(id, role, currentUserId, acessos, asNoTracking: true);
        }

        public int GetOwnerUserId(int id)
        {
            return _repo.Query()
                .AsNoTracking()
                .Where(s => s.Id == id)
                .Select(s => s.Userid)
                .FirstOrDefault();
        }

        public ChamadoTIComunicacaoResponseDto AddComunicacao(int id, ChamadoTIComunicacaoCreateDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            var chamado = GetAccessibleChamado(id, role, currentUserId, acessos);
            if (IsFinalizado(chamado))
                throw new InvalidOperationException("Chamados encerrados ou cancelados nao permitem novas mensagens.");

            if (string.IsNullOrWhiteSpace(chamado.Responsavel))
                throw new InvalidOperationException("Defina um responsavel pelo chamado antes de usar o chat.");

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

        public string BuildComunicacoesDownload(int id, string role, int currentUserId, IEnumerable<string> acessos)
        {
            if (!CanManage(role, acessos))
                throw new UnauthorizedAccessException("Somente usuarios de T.I. podem baixar o historico do chat.");

            var chamado = GetAccessibleChamado(id, role, currentUserId, acessos, asNoTracking: true);
            var mensagens = _repo.QueryComunicacoes()
                .AsNoTracking()
                .Where(s => s.ChamadoTIId == id)
                .OrderBy(s => s.DataCriacao)
                .ToList();

            var builder = new StringBuilder();
            builder.AppendLine($"Historico do chat - Chamado #{chamado.Id}");
            builder.AppendLine($"Titulo: {chamado.Titulo}");
            builder.AppendLine($"Solicitante: {chamado.Solicitante}");
            builder.AppendLine($"Unidade: {chamado.Unidade}");
            builder.AppendLine($"Departamento: {chamado.Departamento}");
            builder.AppendLine($"Status: {chamado.Status}");
            builder.AppendLine($"Gerado em: {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC");
            builder.AppendLine(new string('-', 72));

            if (mensagens.Count == 0)
            {
                builder.AppendLine("Nenhuma mensagem registrada neste chamado.");
                return builder.ToString();
            }

            foreach (var mensagem in mensagens)
            {
                builder.AppendLine($"[{mensagem.DataCriacao:dd/MM/yyyy HH:mm}] {mensagem.AutorNome} ({mensagem.AutorRole})");
                builder.AppendLine(mensagem.Mensagem);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        public ChamadoTIComunicacaoResponseDto MarcarComunicacaoLida(int id, int comunicacaoId, string role, int currentUserId, IEnumerable<string> acessos)
        {
            GetAccessibleChamado(id, role, currentUserId, acessos, asNoTracking: true);

            var comunicacao = _repo.QueryComunicacoes()
                .FirstOrDefault(s => s.Id == comunicacaoId && s.ChamadoTIId == id);

            if (comunicacao is null)
                throw new KeyNotFoundException("Mensagem nao encontrada.");

            if (comunicacao.AutorUserId == currentUserId)
                return MapComunicacao(comunicacao);

            comunicacao.DataLeitura ??= DateTime.UtcNow;
            _repo.UpdateComunicacao(comunicacao);
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
            chamado.DataAprovacao = null;
            chamado.Aprovado = null;
            chamado.ObservacoesAprovacao = string.Empty;

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

            if (chamado.DataAprovacao == null)
                throw new InvalidOperationException("A pesquisa de satisfacao so pode ser preenchida apos a aprovacao do fechamento do chamado.");

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

        public ChamadoTIResponseDto Aprovar(int id, ChamadoTIAprovacaoDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            if (CanManage(role, acessos))
                throw new UnauthorizedAccessException("Administradores apenas visualizam a aprovacao do chamado.");

            var chamado = GetAccessibleChamado(id, role, currentUserId, acessos);
            if (!chamado.DataEncerramento.HasValue)
                throw new InvalidOperationException("A aprovacao so pode ser registrada apos o encerramento do chamado.");

            if (chamado.DataAprovacao.HasValue)
                throw new InvalidOperationException("Este chamado ja foi aprovado.");

            chamado.DataAprovacao = DateTime.UtcNow;
            chamado.Aprovado = true;
            chamado.ObservacoesAprovacao = dto.ObservacoesAprovacao?.Trim() ?? string.Empty;

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
            chamado.DataAprovacao = null;
            chamado.Aprovado = null;
            chamado.ObservacoesAprovacao = string.Empty;

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
                AcessoRemotoSenha = s.AcessoRemotoSenha,
                EquipamentoNome = s.EquipamentoNome,
                EquipamentoIp = s.EquipamentoIp,
                EquipamentoSistemaOperacional = s.EquipamentoSistemaOperacional,
                AnexoImagemUrl = s.AnexoImagemUrl,
                Observacoes = s.Observacoes,
                ObservacoesEncerramento = s.ObservacoesEncerramento,
                SatisfacaoNota = s.SatisfacaoNota,
                SatisfacaoComentario = s.SatisfacaoComentario,
                DataAvaliacao = s.DataAvaliacao,
                DataAprovacao = s.DataAprovacao,
                Aprovado = s.Aprovado,
                ObservacoesAprovacao = s.ObservacoesAprovacao,
                AprovacaoPendente = s.DataEncerramento.HasValue && !s.DataAprovacao.HasValue,
                AvaliacaoPendente = s.DataEncerramento.HasValue && s.DataAprovacao.HasValue && !s.SatisfacaoNota.HasValue,
                DataAbertura = s.DataAbertura,
                DataPrimeiroEncerramento = s.DataPrimeiroEncerramento,
                DataReabertura = s.DataReabertura,
                DataEncerramento = s.DataEncerramento,
                UltimaMovimentacao = GetUltimaMovimentacao(s),
                Reaberto = s.Reaberto
            };
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
                DataCriacao = s.DataCriacao,
                DataLeitura = s.DataLeitura
            };
        }
    }
}
