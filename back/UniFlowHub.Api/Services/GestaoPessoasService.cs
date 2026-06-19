using Microsoft.EntityFrameworkCore;
using UniFlowHub.Api.Data;
using UniFlowHub.Api.Dtos.GestaoPessoas;
using UniFlowHub.Api.Models;
using UniFlowHub.Api.Security;

namespace UniFlowHub.Api.Services
{
    public class GestaoPessoasService
    {
        private const string StatusEmAndamento = "Em andamento";
        private const string StatusConcluido = "Concluido";
        private const string StatusCancelado = "Cancelado";

        private readonly AppDbContext _context;

        public GestaoPessoasService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<GestaoPessoasEtapaDto>> ListEtapasAsync(string? tipoProcesso)
        {
            var query = _context.GestaoPessoasEtapa.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(tipoProcesso))
            {
                var tipo = NormalizeTipo(tipoProcesso);
                query = query.Where(s => s.TipoProcesso == tipo);
            }

            return await query
                .OrderBy(s => s.TipoProcesso)
                .ThenBy(s => s.Ordem)
                .ThenBy(s => s.Nome)
                .Select(s => MapEtapa(s))
                .ToListAsync();
        }

        public async Task<GestaoPessoasEtapaDto> SaveEtapaAsync(int? id, GestaoPessoasEtapaSaveDto dto, string role, IEnumerable<string> acessos)
        {
            EnsureCanManageEtapas(role, acessos);
            var nome = dto.Nome?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nome))
                throw new InvalidOperationException("Nome da etapa e obrigatorio.");

            var tipo = NormalizeTipo(dto.TipoProcesso);
            var ordem = dto.Ordem <= 0 ? 1 : dto.Ordem;
            GestaoPessoasEtapa etapa;

            if (id.HasValue && id.Value > 0)
            {
                etapa = await _context.GestaoPessoasEtapa.FirstOrDefaultAsync(s => s.Id == id.Value)
                    ?? throw new KeyNotFoundException("Etapa nao encontrada.");
                etapa.Nome = nome;
                etapa.TipoProcesso = tipo;
                etapa.Ordem = ordem;
                etapa.Ativa = dto.Ativa;
                etapa.DataAtualizacao = DateTime.UtcNow;
            }
            else
            {
                etapa = new GestaoPessoasEtapa
                {
                    Nome = nome,
                    TipoProcesso = tipo,
                    Ordem = ordem,
                    Ativa = dto.Ativa,
                    DataCadastro = DateTime.UtcNow
                };
                _context.GestaoPessoasEtapa.Add(etapa);
            }

            await _context.SaveChangesAsync();
            return MapEtapa(etapa);
        }

        public async Task DeleteEtapaAsync(int id, string role, IEnumerable<string> acessos)
        {
            EnsureCanManageEtapas(role, acessos);
            var etapa = await _context.GestaoPessoasEtapa.FirstOrDefaultAsync(s => s.Id == id)
                ?? throw new KeyNotFoundException("Etapa nao encontrada.");

            var emUso = await _context.GestaoPessoasProcesso.AnyAsync(s => s.EtapaAtualId == id)
                || await _context.GestaoPessoasProcessoHistorico.AnyAsync(s => s.EtapaId == id);
            if (emUso)
                throw new InvalidOperationException("Esta etapa ja esta em uso. Desative a etapa para remove-la do fluxo.");

            _context.GestaoPessoasEtapa.Remove(etapa);
            await _context.SaveChangesAsync();
        }

        public async Task<List<GestaoPessoasProcessoDto>> ListProcessosAsync(string role, int userId, IEnumerable<string> acessos)
        {
            var query = _context.GestaoPessoasProcesso
                .AsNoTracking()
                .Include(s => s.EtapaAtual)
                .Include(s => s.Historico)
                .ThenInclude(s => s.Etapa)
                .AsQueryable();

            if (!CanManageRH(role, acessos))
                query = query.Where(s => s.Userid == userId);

            return await query
                .OrderByDescending(s => s.DataSolicitacao)
                .Select(s => MapProcesso(s))
                .ToListAsync();
        }

        public async Task<GestaoPessoasProcessoDto> CreateProcessoAsync(GestaoPessoasProcessoCreateDto dto, string role, int currentUserId, IEnumerable<string> acessos)
        {
            EnsureCanMove(role, acessos);
            var ownerUserId = CanManageRH(role, acessos) && dto.Userid > 0 ? dto.Userid : currentUserId;
            if (!await _context.User.AnyAsync(s => s.Id == ownerUserId))
                throw new InvalidOperationException("Usuario solicitante invalido.");

            var tipo = NormalizeTipo(dto.TipoProcesso);
            ValidateProcesso(dto.Titulo, dto.Solicitante, dto.Unidade, dto.Departamento, dto.ColaboradorNome, dto.Descricao);

            var primeiraEtapa = await FirstEtapaAsync(tipo);
            var processo = new GestaoPessoasProcesso
            {
                TipoProcesso = tipo,
                Titulo = dto.Titulo.Trim(),
                Solicitante = dto.Solicitante.Trim(),
                Unidade = dto.Unidade.Trim(),
                Departamento = dto.Departamento.Trim(),
                ColaboradorNome = dto.ColaboradorNome.Trim(),
                Cargo = dto.Cargo?.Trim() ?? string.Empty,
                Descricao = dto.Descricao.Trim(),
                Prioridade = string.IsNullOrWhiteSpace(dto.Prioridade) ? "Media" : dto.Prioridade.Trim(),
                Observacoes = dto.Observacoes?.Trim() ?? string.Empty,
                Status = StatusEmAndamento,
                DataSolicitacao = DateTime.UtcNow,
                DataAprovacaoGestor = DateTime.UtcNow,
                AprovadorGestor = "Fluxo direto",
                EtapaAtualId = primeiraEtapa?.Id,
                Userid = ownerUserId
            };

            _context.GestaoPessoasProcesso.Add(processo);
            if (primeiraEtapa is not null)
                AddHistorico(processo, primeiraEtapa, "Criado", "Processo iniciado diretamente no fluxo.", currentUserId, dto.Solicitante.Trim());
            await _context.SaveChangesAsync();
            return await GetProcessoDtoAsync(processo.Id);
        }

        public async Task<GestaoPessoasProcessoDto> AvancarAsync(int id, GestaoPessoasMovimentoDto dto, string role, int userId, IEnumerable<string> acessos)
        {
            EnsureCanMove(role, acessos);
            var processo = await GetProcessoAsync(id);
            EnsureApprovedAndOpen(processo);

            var etapas = await EtapasAtivasAsync(processo.TipoProcesso);
            var index = etapas.FindIndex(s => s.Id == processo.EtapaAtualId);
            if (index < 0)
                throw new InvalidOperationException("Etapa atual invalida.");

            var usuario = await _context.User.AsNoTracking().FirstOrDefaultAsync(s => s.Id == userId);
            var current = etapas[index];

            if (index == etapas.Count - 1)
            {
                processo.Status = StatusConcluido;
                processo.DataConclusao = DateTime.UtcNow;
                AddHistorico(processo, current, "Concluido", dto.Observacoes?.Trim() ?? string.Empty, userId, usuario?.Nome ?? string.Empty);
            }
            else
            {
                var next = etapas[index + 1];
                processo.EtapaAtualId = next.Id;
                processo.Status = StatusEmAndamento;
                AddHistorico(processo, next, "Avancou", dto.Observacoes?.Trim() ?? string.Empty, userId, usuario?.Nome ?? string.Empty);
            }

            await _context.SaveChangesAsync();
            return await GetProcessoDtoAsync(processo.Id);
        }

        public async Task<GestaoPessoasProcessoDto> VoltarAsync(int id, GestaoPessoasMovimentoDto dto, string role, int userId, IEnumerable<string> acessos)
        {
            EnsureCanMove(role, acessos);
            var processo = await GetProcessoAsync(id);
            EnsureApprovedAndOpen(processo);

            var etapas = await EtapasAtivasAsync(processo.TipoProcesso);
            var index = etapas.FindIndex(s => s.Id == processo.EtapaAtualId);
            if (index <= 0)
                throw new InvalidOperationException("Nao ha etapa anterior para retornar.");

            var usuario = await _context.User.AsNoTracking().FirstOrDefaultAsync(s => s.Id == userId);
            processo.EtapaAtualId = etapas[index - 1].Id;
            AddHistorico(processo, etapas[index - 1], "Voltou", dto.Observacoes?.Trim() ?? string.Empty, userId, usuario?.Nome ?? string.Empty);

            await _context.SaveChangesAsync();
            return await GetProcessoDtoAsync(processo.Id);
        }

        public async Task<GestaoPessoasProcessoDto> CancelarAsync(int id, GestaoPessoasCancelamentoDto dto, string role, int userId, IEnumerable<string> acessos)
        {
            EnsureCanMove(role, acessos);
            var processo = await GetProcessoAsync(id);

            EnsureNotFinalized(processo);
            var motivo = dto.MotivoCancelamento?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(motivo))
                throw new InvalidOperationException("Motivo do cancelamento e obrigatorio.");

            var usuario = await _context.User.AsNoTracking().FirstOrDefaultAsync(s => s.Id == userId);
            processo.Status = StatusCancelado;
            processo.DataCancelamento = DateTime.UtcNow;
            processo.MotivoCancelamento = motivo;
            if (processo.EtapaAtualId.HasValue)
            {
                var etapa = await _context.GestaoPessoasEtapa.AsNoTracking().FirstOrDefaultAsync(s => s.Id == processo.EtapaAtualId.Value);
                if (etapa is not null)
                    AddHistorico(processo, etapa, "Cancelado", motivo, userId, usuario?.Nome ?? string.Empty);
            }

            await _context.SaveChangesAsync();
            return await GetProcessoDtoAsync(processo.Id);
        }

        private async Task<GestaoPessoasProcesso> GetProcessoAsync(int id)
        {
            return await _context.GestaoPessoasProcesso
                .Include(s => s.Historico)
                .FirstOrDefaultAsync(s => s.Id == id)
                ?? throw new KeyNotFoundException("Processo nao encontrado.");
        }

        private async Task<GestaoPessoasProcessoDto> GetProcessoDtoAsync(int id)
        {
            var processo = await _context.GestaoPessoasProcesso
                .AsNoTracking()
                .Include(s => s.EtapaAtual)
                .Include(s => s.Historico)
                .ThenInclude(s => s.Etapa)
                .FirstAsync(s => s.Id == id);
            return MapProcesso(processo);
        }

        private async Task<GestaoPessoasEtapa?> FirstEtapaAsync(string tipoProcesso)
        {
            return await _context.GestaoPessoasEtapa
                .AsNoTracking()
                .Where(s => s.TipoProcesso == tipoProcesso && s.Ativa)
                .OrderBy(s => s.Ordem)
                .ThenBy(s => s.Nome)
                .FirstOrDefaultAsync();
        }

        private async Task<List<GestaoPessoasEtapa>> EtapasAtivasAsync(string tipoProcesso)
        {
            return await _context.GestaoPessoasEtapa
                .AsNoTracking()
                .Where(s => s.TipoProcesso == tipoProcesso && s.Ativa)
                .OrderBy(s => s.Ordem)
                .ThenBy(s => s.Nome)
                .ToListAsync();
        }

        private static void AddHistorico(GestaoPessoasProcesso processo, GestaoPessoasEtapa etapa, string acao, string observacoes, int userId, string userName)
        {
            processo.Historico.Add(new GestaoPessoasProcessoHistorico
            {
                EtapaId = etapa.Id,
                Acao = acao,
                Observacoes = observacoes,
                UsuarioId = userId,
                UsuarioNome = userName,
                DataMovimentacao = DateTime.UtcNow
            });
        }

        private static string NormalizeTipo(string tipoProcesso)
        {
            var value = tipoProcesso?.Trim().ToLowerInvariant();
            return value switch
            {
                "admissao" or "admissão" => "Admissao",
                "demissao" or "demissão" => "Demissao",
                _ => throw new InvalidOperationException("Tipo de processo invalido.")
            };
        }

        private static void ValidateProcesso(string titulo, string solicitante, string unidade, string departamento, string colaboradorNome, string descricao)
        {
            if (string.IsNullOrWhiteSpace(titulo))
                throw new InvalidOperationException("Titulo e obrigatorio.");
            if (string.IsNullOrWhiteSpace(solicitante))
                throw new InvalidOperationException("Solicitante e obrigatorio.");
            if (string.IsNullOrWhiteSpace(unidade))
                throw new InvalidOperationException("Unidade e obrigatoria.");
            if (string.IsNullOrWhiteSpace(departamento))
                throw new InvalidOperationException("Departamento e obrigatorio.");
            if (string.IsNullOrWhiteSpace(colaboradorNome))
                throw new InvalidOperationException("Nome do colaborador e obrigatorio.");
            if (string.IsNullOrWhiteSpace(descricao))
                throw new InvalidOperationException("Descricao e obrigatoria.");
        }

        private static void EnsureApprovedAndOpen(GestaoPessoasProcesso processo)
        {
            EnsureNotFinalized(processo);
            if (!processo.EtapaAtualId.HasValue)
                throw new InvalidOperationException("Cadastre ao menos uma etapa ativa para este processo.");
        }

        private static void EnsureNotFinalized(GestaoPessoasProcesso processo)
        {
            if (processo.Status == StatusCancelado || processo.Status == StatusConcluido || processo.DataCancelamento.HasValue || processo.DataConclusao.HasValue)
                throw new InvalidOperationException("Processo finalizado nao permite movimentacao.");
        }

        private static void EnsureCanManageEtapas(string role, IEnumerable<string> acessos)
        {
            if (!CanManageRH(role, acessos))
                throw new UnauthorizedAccessException("Somente RH pode cadastrar etapas.");
        }

        private static void EnsureCanMove(string role, IEnumerable<string> acessos)
        {
            if (!CanManageRH(role, acessos))
                throw new UnauthorizedAccessException("Somente usuarios de RH podem movimentar processos.");
        }

        private static bool CanManageRH(string role, IEnumerable<string> acessos)
        {
            return RoleScope.IsAdmin(role) || RoleScope.IsRH(role) || HasAccess(acessos, "rh-admin");
        }

        private static bool HasAccess(IEnumerable<string> acessos, string chave)
            => acessos.Any(acesso => string.Equals(acesso, chave, StringComparison.OrdinalIgnoreCase));

        private static GestaoPessoasEtapaDto MapEtapa(GestaoPessoasEtapa s)
        {
            return new GestaoPessoasEtapaDto
            {
                Id = s.Id,
                Nome = s.Nome,
                TipoProcesso = s.TipoProcesso,
                Ordem = s.Ordem,
                Ativa = s.Ativa,
                DataCadastro = s.DataCadastro,
                DataAtualizacao = s.DataAtualizacao
            };
        }

        private static GestaoPessoasProcessoDto MapProcesso(GestaoPessoasProcesso s)
        {
            return new GestaoPessoasProcessoDto
            {
                Id = s.Id,
                TipoProcesso = s.TipoProcesso,
                Titulo = s.Titulo,
                Solicitante = s.Solicitante,
                Unidade = s.Unidade,
                Departamento = s.Departamento,
                ColaboradorNome = s.ColaboradorNome,
                Cargo = s.Cargo,
                Descricao = s.Descricao,
                Prioridade = s.Prioridade,
                Status = s.Status,
                Observacoes = s.Observacoes,
                DataSolicitacao = s.DataSolicitacao,
                DataAprovacaoGestor = s.DataAprovacaoGestor,
                AprovadorGestor = s.AprovadorGestor,
                ObservacoesAprovacao = s.ObservacoesAprovacao,
                DataCancelamento = s.DataCancelamento,
                MotivoCancelamento = s.MotivoCancelamento,
                DataConclusao = s.DataConclusao,
                EtapaAtualId = s.EtapaAtualId,
                EtapaAtualNome = s.EtapaAtual?.Nome ?? string.Empty,
                Userid = s.Userid,
                Historico = s.Historico
                    .OrderBy(h => h.DataMovimentacao)
                    .Select(h => new GestaoPessoasProcessoHistoricoDto
                    {
                        Id = h.Id,
                        EtapaId = h.EtapaId,
                        EtapaNome = h.Etapa?.Nome ?? string.Empty,
                        Acao = h.Acao,
                        Observacoes = h.Observacoes,
                        UsuarioId = h.UsuarioId,
                        UsuarioNome = h.UsuarioNome,
                        DataMovimentacao = h.DataMovimentacao
                    })
                    .ToList()
            };
        }
    }
}
