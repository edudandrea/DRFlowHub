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

        public async Task<List<GestaoPessoasCargoDto>> ListCargosAsync()
        {
            await EnsureCargosFromUsuariosAsync();

            return await _context.GestaoPessoasCargo
                .AsNoTracking()
                .Include(s => s.Itens)
                .ThenInclude(s => s.Item)
                .Include(s => s.Acessos)
                .OrderBy(s => s.Nome)
                .Select(s => MapCargo(s))
                .ToListAsync();
        }

        public async Task<GestaoPessoasCargoDto> SaveCargoAsync(int? id, GestaoPessoasCargoSaveDto dto, string role, IEnumerable<string> acessos)
        {
            EnsureCanManageCargos(role, acessos);
            var nome = dto.Nome?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nome))
                throw new InvalidOperationException("Nome do cargo e obrigatorio.");

            GestaoPessoasCargo cargo;
            if (id.HasValue && id.Value > 0)
            {
                cargo = await _context.GestaoPessoasCargo
                    .Include(s => s.Itens)
                    .FirstOrDefaultAsync(s => s.Id == id.Value)
                    ?? throw new KeyNotFoundException("Cargo nao encontrado.");
                await _context.Entry(cargo).Collection(s => s.Acessos).LoadAsync();
                cargo.Nome = nome;
                cargo.Departamento = dto.Departamento?.Trim() ?? string.Empty;
                cargo.Descricao = dto.Descricao?.Trim() ?? string.Empty;
                cargo.Ativo = dto.Ativo;
                cargo.DataAtualizacao = DateTime.UtcNow;
                _context.GestaoPessoasCargoItem.RemoveRange(cargo.Itens);
                _context.GestaoPessoasCargoAcesso.RemoveRange(cargo.Acessos);
            }
            else
            {
                cargo = new GestaoPessoasCargo
                {
                    Nome = nome,
                    Departamento = dto.Departamento?.Trim() ?? string.Empty,
                    Descricao = dto.Descricao?.Trim() ?? string.Empty,
                    Ativo = dto.Ativo,
                    DataCadastro = DateTime.UtcNow
                };
                _context.GestaoPessoasCargo.Add(cargo);
            }

            foreach (var item in NormalizeCargoItens(dto.Itens))
            {
                cargo.Itens.Add(new GestaoPessoasCargoItem
                {
                    ItemId = item.ItemId,
                    Quantidade = item.Quantidade <= 0 ? 1 : item.Quantidade,
                    Obrigatorio = item.Obrigatorio
                });
            }
            foreach (var acesso in NormalizeCargoAcessos(dto.Acessos))
                cargo.Acessos.Add(new GestaoPessoasCargoAcesso { Chave = acesso });

            await _context.SaveChangesAsync();
            return await GetCargoDtoAsync(cargo.Id);
        }

        public async Task<List<GestaoPessoasItemDto>> ListItensAsync(string? tipo)
        {
            var query = _context.GestaoPessoasItem.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(tipo))
            {
                var normalized = NormalizeItemTipo(tipo);
                query = query.Where(s => s.Tipo == normalized);
            }

            return await query
                .OrderBy(s => s.Tipo)
                .ThenBy(s => s.Nome)
                .Select(s => MapItem(s))
                .ToListAsync();
        }

        public async Task<GestaoPessoasItemDto> SaveItemAsync(int? id, GestaoPessoasItemSaveDto dto, string role, IEnumerable<string> acessos)
        {
            EnsureCanMove(role, acessos);
            var nome = dto.Nome?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nome))
                throw new InvalidOperationException("Nome do EPI ou uniforme e obrigatorio.");

            var tipo = NormalizeItemTipo(dto.Tipo);
            GestaoPessoasItem item;
            if (id.HasValue && id.Value > 0)
            {
                item = await _context.GestaoPessoasItem.FirstOrDefaultAsync(s => s.Id == id.Value)
                    ?? throw new KeyNotFoundException("Item nao encontrado.");
                item.Tipo = tipo;
                item.Nome = nome;
                item.Codigo = dto.Codigo?.Trim() ?? string.Empty;
                item.Tamanho = dto.Tamanho?.Trim() ?? string.Empty;
                item.Descricao = dto.Descricao?.Trim() ?? string.Empty;
                item.Ativo = dto.Ativo;
                item.DataAtualizacao = DateTime.UtcNow;
            }
            else
            {
                item = new GestaoPessoasItem
                {
                    Tipo = tipo,
                    Nome = nome,
                    Codigo = dto.Codigo?.Trim() ?? string.Empty,
                    Tamanho = dto.Tamanho?.Trim() ?? string.Empty,
                    Descricao = dto.Descricao?.Trim() ?? string.Empty,
                    Ativo = dto.Ativo,
                    DataCadastro = DateTime.UtcNow
                };
                _context.GestaoPessoasItem.Add(item);
            }

            await _context.SaveChangesAsync();
            return MapItem(item);
        }

        public async Task<List<GestaoPessoasColaboradorDto>> ListColaboradoresAsync()
        {
            await EnsureColaboradoresFromUsuariosAsync();

            return await _context.GestaoPessoasColaborador
                .AsNoTracking()
                .Include(s => s.Cargo)
                .ThenInclude(s => s!.Itens)
                .ThenInclude(s => s.Item)
                .Include(s => s.Unidade)
                .Include(s => s.Retiradas)
                .ThenInclude(s => s.Item)
                .OrderBy(s => s.Nome)
                .Select(s => MapColaborador(s))
                .ToListAsync();
        }

        public async Task<GestaoPessoasColaboradorDto> SaveColaboradorAsync(int? id, GestaoPessoasColaboradorSaveDto dto, string role, IEnumerable<string> acessos)
        {
            EnsureCanMove(role, acessos);
            var nome = dto.Nome?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nome))
                throw new InvalidOperationException("Nome do colaborador e obrigatorio.");

            if (dto.CargoId.HasValue && !await _context.GestaoPessoasCargo.AnyAsync(s => s.Id == dto.CargoId.Value))
                throw new InvalidOperationException("Cargo invalido.");

            GestaoPessoasColaborador colaborador;
            if (id.HasValue && id.Value > 0)
            {
                colaborador = await _context.GestaoPessoasColaborador.FirstOrDefaultAsync(s => s.Id == id.Value)
                    ?? throw new KeyNotFoundException("Colaborador nao encontrado.");
                colaborador.DataAtualizacao = DateTime.UtcNow;
            }
            else
            {
                colaborador = new GestaoPessoasColaborador { DataCadastro = DateTime.UtcNow };
                _context.GestaoPessoasColaborador.Add(colaborador);
            }

            colaborador.Nome = nome;
            colaborador.Cpf = dto.Cpf?.Trim() ?? string.Empty;
            colaborador.Email = dto.Email?.Trim() ?? string.Empty;
            colaborador.Telefone = dto.Telefone?.Trim() ?? string.Empty;
            colaborador.Departamento = dto.Departamento?.Trim() ?? string.Empty;
            colaborador.CargoId = dto.CargoId > 0 ? dto.CargoId : null;
            colaborador.UnidadeId = dto.UnidadeId > 0 ? dto.UnidadeId : null;
            colaborador.DataNascimento = dto.DataNascimento;
            colaborador.DataAdmissao = dto.DataAdmissao;
            colaborador.Status = string.IsNullOrWhiteSpace(dto.Status) ? "Ativo" : dto.Status.Trim();
            colaborador.Observacoes = dto.Observacoes?.Trim() ?? string.Empty;

            await _context.SaveChangesAsync();
            await EnsureUsuarioForColaboradorAsync(colaborador);

            return await GetColaboradorDtoAsync(colaborador.Id);
        }

        public async Task<GestaoPessoasColaboradorRetiradaDto> AddRetiradaAsync(int colaboradorId, GestaoPessoasColaboradorRetiradaSaveDto dto, string role, IEnumerable<string> acessos)
        {
            EnsureCanMove(role, acessos);
            if (!await _context.GestaoPessoasColaborador.AnyAsync(s => s.Id == colaboradorId))
                throw new InvalidOperationException("Colaborador invalido.");
            if (!await _context.GestaoPessoasItem.AnyAsync(s => s.Id == dto.ItemId))
                throw new InvalidOperationException("EPI ou uniforme invalido.");

            var retirada = new GestaoPessoasColaboradorRetirada
            {
                ColaboradorId = colaboradorId,
                ItemId = dto.ItemId,
                Quantidade = dto.Quantidade <= 0 ? 1 : dto.Quantidade,
                DataRetirada = dto.DataRetirada ?? DateTime.UtcNow,
                DataDevolucao = dto.DataDevolucao,
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Retirado" : dto.Status.Trim(),
                Observacoes = dto.Observacoes?.Trim() ?? string.Empty
            };

            _context.GestaoPessoasColaboradorRetirada.Add(retirada);
            await _context.SaveChangesAsync();

            retirada = await _context.GestaoPessoasColaboradorRetirada
                .AsNoTracking()
                .Include(s => s.Item)
                .FirstAsync(s => s.Id == retirada.Id);
            return MapRetirada(retirada);
        }

        private async Task<GestaoPessoasCargoDto> GetCargoDtoAsync(int id)
        {
            var cargo = await _context.GestaoPessoasCargo
                .AsNoTracking()
                .Include(s => s.Itens)
                .ThenInclude(s => s.Item)
                .Include(s => s.Acessos)
                .FirstAsync(s => s.Id == id);
            return MapCargo(cargo);
        }

        private async Task EnsureCargosFromUsuariosAsync()
        {
            var existing = await _context.GestaoPessoasCargo
                .Select(cargo => cargo.Nome)
                .ToListAsync();
            var existingSet = existing
                .Select(NormalizeCargoKey)
                .Where(nome => !string.IsNullOrWhiteSpace(nome))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var cargosUsuarios = await _context.User
                .AsNoTracking()
                .Where(user => user.Cargo != "")
                .Select(user => new { user.Cargo, user.Departamento })
                .ToListAsync();

            foreach (var item in cargosUsuarios
                .Select(user => new
                {
                    Nome = user.Cargo.Trim(),
                    Departamento = user.Departamento?.Trim() ?? string.Empty,
                    Key = NormalizeCargoKey(user.Cargo)
                })
                .Where(user => !string.IsNullOrWhiteSpace(user.Nome))
                .GroupBy(user => user.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()))
            {
                if (string.IsNullOrWhiteSpace(item.Key) || existingSet.Contains(item.Key))
                    continue;

                _context.GestaoPessoasCargo.Add(new GestaoPessoasCargo
                {
                    Nome = item.Nome,
                    Departamento = item.Departamento,
                    Descricao = "Importado do cadastro de usuarios.",
                    Ativo = true,
                    DataCadastro = DateTime.UtcNow
                });
                existingSet.Add(item.Key);
            }

            await _context.SaveChangesAsync();
        }

        private async Task EnsureColaboradoresFromUsuariosAsync()
        {
            await EnsureCargosFromUsuariosAsync();

            var colaboradores = await _context.GestaoPessoasColaborador
                .AsNoTracking()
                .Select(colaborador => new { colaborador.Cpf, colaborador.Email })
                .ToListAsync();
            var cpfs = colaboradores
                .Select(colaborador => colaborador.Cpf.Trim())
                .Where(cpf => !string.IsNullOrWhiteSpace(cpf))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var emails = colaboradores
                .Select(colaborador => colaborador.Email.Trim())
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var cargos = await _context.GestaoPessoasCargo
                .AsNoTracking()
                .Where(cargo => cargo.Ativo)
                .Select(cargo => new { cargo.Id, cargo.Nome })
                .ToListAsync();

            var users = await _context.User
                .AsNoTracking()
                .Where(user => user.Ativo)
                .Select(user => new
                {
                    user.Nome,
                    user.Cpf,
                    user.Email,
                    user.Departamento,
                    user.Cargo,
                    user.UnidadeId,
                    user.DataNascimento
                })
                .ToListAsync();

            foreach (var user in users)
            {
                var cpf = user.Cpf.Trim();
                var email = user.Email.Trim().ToLowerInvariant();
                var existsByCpf = !string.IsNullOrWhiteSpace(cpf) && cpfs.Contains(cpf);
                var existsByEmail = !string.IsNullOrWhiteSpace(email) && emails.Contains(email);
                if (existsByCpf || existsByEmail)
                    continue;

                var cargoId = cargos.FirstOrDefault(cargo => string.Equals(cargo.Nome, user.Cargo.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;
                _context.GestaoPessoasColaborador.Add(new GestaoPessoasColaborador
                {
                    Nome = user.Nome.Trim(),
                    Cpf = cpf,
                    Email = email,
                    Departamento = user.Departamento.Trim(),
                    CargoId = cargoId,
                    UnidadeId = user.UnidadeId,
                    DataNascimento = user.DataNascimento,
                    Status = "Ativo",
                    Observacoes = "Importado do cadastro de usuarios.",
                    DataCadastro = DateTime.UtcNow
                });

                if (!string.IsNullOrWhiteSpace(cpf))
                    cpfs.Add(cpf);
                if (!string.IsNullOrWhiteSpace(email))
                    emails.Add(email);
            }

            await _context.SaveChangesAsync();
        }

        private async Task EnsureUsuarioForColaboradorAsync(GestaoPessoasColaborador colaborador)
        {
            var email = colaborador.Email.Trim().ToLowerInvariant();
            var cpf = colaborador.Cpf.Trim();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(cpf))
                return;

            var exists = await _context.User.AnyAsync(user => user.Email == email || user.Cpf == cpf);
            if (exists)
                return;

            var cargoNome = string.Empty;
            if (colaborador.CargoId.HasValue)
            {
                cargoNome = await _context.GestaoPessoasCargo
                    .Where(cargo => cargo.Id == colaborador.CargoId.Value)
                    .Select(cargo => cargo.Nome)
                    .FirstOrDefaultAsync() ?? string.Empty;
            }

            _context.User.Add(new Users
            {
                Nome = colaborador.Nome.Trim(),
                Cpf = cpf,
                Email = email,
                Senha = PasswordHasher.Hash("123456"),
                Role = "Usuario",
                Departamento = colaborador.Departamento.Trim(),
                Cargo = cargoNome,
                Ativo = string.Equals(colaborador.Status, "Ativo", StringComparison.OrdinalIgnoreCase),
                UnidadeId = colaborador.UnidadeId,
                DataNascimento = colaborador.DataNascimento ?? DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        private async Task<GestaoPessoasColaboradorDto> GetColaboradorDtoAsync(int id)
        {
            var colaborador = await _context.GestaoPessoasColaborador
                .AsNoTracking()
                .Include(s => s.Cargo)
                .ThenInclude(s => s!.Itens)
                .ThenInclude(s => s.Item)
                .Include(s => s.Unidade)
                .Include(s => s.Retiradas)
                .ThenInclude(s => s.Item)
                .FirstAsync(s => s.Id == id);
            return MapColaborador(colaborador);
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

        private static string NormalizeItemTipo(string tipo)
        {
            var value = tipo?.Trim().ToLowerInvariant();
            return value switch
            {
                "epi" => "EPI",
                "uniforme" => "Uniforme",
                _ => throw new InvalidOperationException("Tipo de item invalido.")
            };
        }

        private static List<GestaoPessoasCargoItemSaveDto> NormalizeCargoItens(IEnumerable<GestaoPessoasCargoItemSaveDto>? itens)
        {
            return (itens ?? Array.Empty<GestaoPessoasCargoItemSaveDto>())
                .Where(s => s.ItemId > 0)
                .GroupBy(s => s.ItemId)
                .Select(s => s.First())
                .ToList();
        }

        private static List<string> NormalizeCargoAcessos(IEnumerable<string>? acessos)
        {
            var validKeys = PerfisService.AcessosDisponiveis
                .Select(a => a.Chave)
                .Where(chave => !string.Equals(chave, "perfis", StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return (acessos ?? Array.Empty<string>())
                .Select(PerfisService.NormalizeAcessoChave)
                .Where(validKeys.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(acesso => acesso)
                .ToList();
        }

        private static string NormalizeCargoKey(string? value)
        {
            return string.Join(' ', (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
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
            if (!CanManageGestaoPessoas(role, acessos))
                throw new UnauthorizedAccessException("Somente usuarios de RH podem movimentar processos.");
        }

        private static void EnsureCanManageCargos(string role, IEnumerable<string> acessos)
        {
            if (!RoleScope.IsAdmin(role)
                && !RoleScope.IsTI(role)
                && !HasAccess(acessos, "usuarios")
                && !HasAccess(acessos, "empresas-revendas"))
                throw new UnauthorizedAccessException("Somente administradores podem gerenciar cargos.");
        }

        private static bool CanManageRH(string role, IEnumerable<string> acessos)
        {
            return RoleScope.IsAdmin(role) || RoleScope.IsRH(role) || HasAccess(acessos, "rh-admin");
        }

        private static bool CanManageGestaoPessoas(string role, IEnumerable<string> acessos)
        {
            return CanManageRH(role, acessos)
                || HasAccess(acessos, "rh")
                || HasAccess(acessos, "gestao-pessoas")
                || HasAccess(acessos, "gestao-pessoas-admin")
                || HasAccess(acessos, "cartao-ponto");
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

        private static GestaoPessoasItemDto MapItem(GestaoPessoasItem s)
        {
            return new GestaoPessoasItemDto
            {
                Id = s.Id,
                Tipo = s.Tipo,
                Nome = s.Nome,
                Codigo = s.Codigo,
                Tamanho = s.Tamanho,
                Descricao = s.Descricao,
                Ativo = s.Ativo,
                DataCadastro = s.DataCadastro,
                DataAtualizacao = s.DataAtualizacao
            };
        }

        private static GestaoPessoasCargoDto MapCargo(GestaoPessoasCargo s)
        {
            return new GestaoPessoasCargoDto
            {
                Id = s.Id,
                Nome = s.Nome,
                Departamento = s.Departamento,
                Descricao = s.Descricao,
                Ativo = s.Ativo,
                DataCadastro = s.DataCadastro,
                DataAtualizacao = s.DataAtualizacao,
                Acessos = s.Acessos
                    .Select(a => PerfisService.NormalizeAcessoChave(a.Chave))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(a => a)
                    .ToList(),
                Itens = s.Itens
                    .OrderBy(i => i.Item?.Tipo)
                    .ThenBy(i => i.Item?.Nome)
                    .Select(MapCargoItem)
                    .ToList()
            };
        }

        private static GestaoPessoasCargoItemDto MapCargoItem(GestaoPessoasCargoItem s)
        {
            return new GestaoPessoasCargoItemDto
            {
                Id = s.Id,
                ItemId = s.ItemId,
                ItemNome = s.Item?.Nome ?? string.Empty,
                ItemTipo = s.Item?.Tipo ?? string.Empty,
                ItemCodigo = s.Item?.Codigo ?? string.Empty,
                ItemTamanho = s.Item?.Tamanho ?? string.Empty,
                Quantidade = s.Quantidade,
                Obrigatorio = s.Obrigatorio
            };
        }

        private static GestaoPessoasColaboradorDto MapColaborador(GestaoPessoasColaborador s)
        {
            return new GestaoPessoasColaboradorDto
            {
                Id = s.Id,
                Nome = s.Nome,
                Cpf = s.Cpf,
                Email = s.Email,
                Telefone = s.Telefone,
                Departamento = s.Departamento,
                CargoId = s.CargoId,
                CargoNome = s.Cargo?.Nome ?? string.Empty,
                UnidadeId = s.UnidadeId,
                UnidadeNome = s.Unidade?.Nome ?? string.Empty,
                DataNascimento = s.DataNascimento,
                DataAdmissao = s.DataAdmissao,
                Status = s.Status,
                Observacoes = s.Observacoes,
                DataCadastro = s.DataCadastro,
                DataAtualizacao = s.DataAtualizacao,
                ItensDoCargo = s.Cargo?.Itens
                    .OrderBy(i => i.Item?.Tipo)
                    .ThenBy(i => i.Item?.Nome)
                    .Select(MapCargoItem)
                    .ToList() ?? new List<GestaoPessoasCargoItemDto>(),
                Retiradas = s.Retiradas
                    .OrderByDescending(r => r.DataRetirada)
                    .Select(MapRetirada)
                    .ToList()
            };
        }

        private static GestaoPessoasColaboradorRetiradaDto MapRetirada(GestaoPessoasColaboradorRetirada s)
        {
            return new GestaoPessoasColaboradorRetiradaDto
            {
                Id = s.Id,
                ColaboradorId = s.ColaboradorId,
                ItemId = s.ItemId,
                ItemNome = s.Item?.Nome ?? string.Empty,
                ItemTipo = s.Item?.Tipo ?? string.Empty,
                ItemCodigo = s.Item?.Codigo ?? string.Empty,
                ItemTamanho = s.Item?.Tamanho ?? string.Empty,
                Quantidade = s.Quantidade,
                DataRetirada = s.DataRetirada,
                DataDevolucao = s.DataDevolucao,
                Status = s.Status,
                Observacoes = s.Observacoes
            };
        }
    }
}
