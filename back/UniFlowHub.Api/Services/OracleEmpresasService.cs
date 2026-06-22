using Oracle.ManagedDataAccess.Client;
using UniFlowHub.Api.Dtos.Unidades;
using Microsoft.EntityFrameworkCore;
using UniFlowHub.Api.Data;
using System.Data.Common;

namespace UniFlowHub.Api.Services
{
    public class OracleEmpresasService
    {
        private readonly string _connectionString;
        private readonly AppDbContext _context;

        public OracleEmpresasService(IConfiguration configuration, AppDbContext context)
        {
            _connectionString = GetOracleConnectionString(configuration);
            _context = context;
        }

        public async Task<List<UnidadeOracleResponseDto>> ListRevendasAsync(string role = "", bool includeInativas = false)
        {
            var revendas = new List<OracleRevendaDto>();

            try
            {
                EnsureConnectionString();

                await using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        gr.EMPRESA as EmpresaNumero,
                        COALESCE(gm.NOME_MARCA, TO_CHAR(gr.MARCA)) as EmpresaNome,
                        gr.REVENDA as NumeroRevenda,
                        gr.RAZAO_SOCIAL as NomeRevenda,
                        gr.RAZAO_SOCIAL as RazaoSocial,
                        gr.CNPJ,
                        gr.ENDERECO
                    FROM GER_REVENDA gr
                    LEFT JOIN GER_MARCA gm ON gm.MARCA = gr.MARCA
                    ORDER BY gr.EMPRESA, gr.REVENDA";

                await using var command = connection.CreateCommand();
                command.CommandText = query;

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    revendas.Add(new OracleRevendaDto
                    {
                        EmpresaNumero = GetInt(reader, "EmpresaNumero"),
                        EmpresaNome = GetString(reader, "EmpresaNome"),
                        NumeroRevenda = GetInt(reader, "NumeroRevenda"),
                        NomeRevenda = GetString(reader, "NomeRevenda"),
                        RazaoSocial = GetString(reader, "RazaoSocial"),
                        Cnpj = NormalizeCnpj(GetString(reader, "CNPJ")),
                        Endereco = GetString(reader, "Endereco")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Erro ao buscar revendas do Oracle.", ex);
            }

            var empresasPermitidas = GetEmpresasPermitidas(role);
            if (empresasPermitidas.Count > 0)
                revendas = revendas.Where(r => empresasPermitidas.Contains(r.EmpresaNumero)).ToList();

            var configs = await _context.MontadoraRevendaConfig
                .AsNoTracking()
                .ToDictionaryAsync(c => BuildConfigKey(c.EmpresaNumero, c.RevendaNumero));

            var empresaStatus = await _context.EmpresaRevendaStatusConfig
                .AsNoTracking()
                .ToDictionaryAsync(c => c.EmpresaNumero);

            var resultado = new List<UnidadeOracleResponseDto>();
            foreach (var revenda in revendas)
            {
                configs.TryGetValue(BuildConfigKey(revenda.EmpresaNumero, revenda.NumeroRevenda), out var config);
                empresaStatus.TryGetValue(revenda.EmpresaNumero, out var empresaConfig);
                var empresaAtiva = empresaConfig?.Ativa ?? true;
                var revendaAtiva = config?.Ativa ?? true;
                var ativa = empresaAtiva && revendaAtiva;

                if (!includeInativas && !ativa)
                    continue;

                resultado.Add(new UnidadeOracleResponseDto
                {
                    Id = config?.Id ?? BuildStableId(revenda.EmpresaNumero, revenda.NumeroRevenda),
                    Nome = $"{revenda.EmpresaNome} - {revenda.NomeRevenda}",
                    EmpresaNumero = revenda.EmpresaNumero,
                    EmpresaNome = revenda.EmpresaNome,
                    NumeroRevenda = revenda.NumeroRevenda,
                    NomeRevenda = revenda.NomeRevenda,
                    RazaoSocial = revenda.RazaoSocial,
                    Cnpj = revenda.Cnpj,
                    Endereco = revenda.Endereco,
                    Montadora = config?.Montadora ?? string.Empty,
                    LogoMontadoraUrl = config?.LogoMontadoraUrl,
                    Ativa = ativa,
                    EmpresaAtiva = empresaAtiva,
                    RevendaAtiva = revendaAtiva
                });
            }

            return resultado.OrderBy(r => r.EmpresaNome).ThenBy(r => r.NumeroRevenda).ToList();
        }

        /// <summary>
        /// Atualiza a montadora e logo de uma revenda
        /// </summary>
        public async Task<UnidadeOracleResponseDto> UpdateMontadoraAsync(
            int empresaNumero, 
            int revendaNumero, 
            string montadora, 
            string? logoUrl)
        {
            EnsureConnectionString();

            // Buscar revenda do Oracle para ter dados atualizados
            var revenda = await GetRevendaFromOracleAsync(empresaNumero, revendaNumero);
            if (revenda == null)
                throw new KeyNotFoundException("Revenda nao encontrada no Oracle.");

            var normalizedLogo = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
            var normalizedMontadora = string.IsNullOrWhiteSpace(montadora) ? string.Empty : montadora.Trim();

            var config = await _context.MontadoraRevendaConfig
                .FirstOrDefaultAsync(u => u.EmpresaNumero == empresaNumero && u.RevendaNumero == revendaNumero);

            if (config == null)
            {
                config = new Models.MontadoraRevendaConfig
                {
                    EmpresaNumero = empresaNumero,
                    RevendaNumero = revendaNumero,
                    Montadora = normalizedMontadora,
                    LogoMontadoraUrl = normalizedLogo,
                    Ativa = true,
                    DataAtualizacao = DateTime.UtcNow
                };
                _context.MontadoraRevendaConfig.Add(config);
            }
            else
            {
                config.Montadora = normalizedMontadora;
                config.LogoMontadoraUrl = normalizedLogo;
                config.DataAtualizacao = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return new UnidadeOracleResponseDto
            {
                Id = config.Id,
                Nome = $"{revenda.EmpresaNome} - {revenda.NomeRevenda}",
                EmpresaNumero = revenda.EmpresaNumero,
                EmpresaNome = revenda.EmpresaNome,
                NumeroRevenda = revenda.NumeroRevenda,
                NomeRevenda = revenda.NomeRevenda,
                RazaoSocial = revenda.RazaoSocial,
                Cnpj = revenda.Cnpj,
                Endereco = revenda.Endereco,
                Montadora = normalizedMontadora,
                LogoMontadoraUrl = normalizedLogo,
                Ativa = config.Ativa,
                EmpresaAtiva = await IsEmpresaAtivaAsync(empresaNumero),
                RevendaAtiva = config.Ativa
            };
        }

        public async Task<UnidadeOracleResponseDto> UpdateEmpresaStatusAsync(int empresaNumero, bool ativa)
        {
            var config = await _context.EmpresaRevendaStatusConfig
                .FirstOrDefaultAsync(c => c.EmpresaNumero == empresaNumero);

            if (config == null)
            {
                config = new Models.EmpresaRevendaStatusConfig
                {
                    EmpresaNumero = empresaNumero,
                    Ativa = ativa,
                    DataAtualizacao = DateTime.UtcNow
                };
                _context.EmpresaRevendaStatusConfig.Add(config);
            }
            else
            {
                config.Ativa = ativa;
                config.DataAtualizacao = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            var revenda = (await ListRevendasAsync(includeInativas: true))
                .FirstOrDefault(r => r.EmpresaNumero == empresaNumero);
            return revenda ?? throw new KeyNotFoundException("Empresa nao encontrada no Oracle.");
        }

        public async Task<UnidadeOracleResponseDto> UpdateRevendaStatusAsync(int empresaNumero, int revendaNumero, bool ativa)
        {
            var revenda = await GetRevendaFromOracleAsync(empresaNumero, revendaNumero);
            if (revenda == null)
                throw new KeyNotFoundException("Revenda nao encontrada no Oracle.");

            var config = await _context.MontadoraRevendaConfig
                .FirstOrDefaultAsync(c => c.EmpresaNumero == empresaNumero && c.RevendaNumero == revendaNumero);

            if (config == null)
            {
                config = new Models.MontadoraRevendaConfig
                {
                    EmpresaNumero = empresaNumero,
                    RevendaNumero = revendaNumero,
                    Montadora = string.Empty,
                    Ativa = ativa,
                    DataAtualizacao = DateTime.UtcNow
                };
                _context.MontadoraRevendaConfig.Add(config);
            }
            else
            {
                config.Ativa = ativa;
                config.DataAtualizacao = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return (await ListRevendasAsync(includeInativas: true))
                .First(r => r.EmpresaNumero == empresaNumero && r.NumeroRevenda == revendaNumero);
        }

        public async Task<List<EmpresaResponseDto>> ListEmpresasAsync(string role = "")
        {
            var revendas = await ListRevendasAsync(role);
            return revendas
                .GroupBy(r => new { r.EmpresaNumero, r.EmpresaNome })
                .OrderBy(g => g.Key.EmpresaNumero)
                .Select(g =>
                {
                    var logo = g.Select(r => r.LogoMontadoraUrl).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
                    return new EmpresaResponseDto
                    {
                        Id = g.Key.EmpresaNumero,
                        Numero = g.Key.EmpresaNumero,
                        Nome = g.Key.EmpresaNome,
                        LogoUrl = logo,
                        DataCadastro = DateTime.MinValue
                    };
                })
                .ToList();
        }

        public async Task<List<UnidadeResponseDto>> ListUnidadesAsync(string role = "")
        {
            var revendas = await ListRevendasAsync(role);
            return revendas
                .Select(r => new UnidadeResponseDto
                {
                    Id = BuildStableId(r.EmpresaNumero, r.NumeroRevenda),
                    Nome = r.Nome,
                    EmpresaId = r.EmpresaNumero,
                    EmpresaNumero = r.EmpresaNumero,
                    NumeroRevenda = r.NumeroRevenda,
                    Empresa = r.EmpresaNome,
                    Revenda = r.NomeRevenda,
                    Cnpj = r.Cnpj,
                    Endereco = r.Endereco,
                    DataCadastro = DateTime.MinValue
                })
                .ToList();
        }

        private async Task<OracleRevendaDto?> GetRevendaFromOracleAsync(int empresaNumero, int revendaNumero)
        {
            try
            {
                EnsureConnectionString();

                await using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        gr.EMPRESA as EmpresaNumero,
                        COALESCE(gm.NOME_MARCA, TO_CHAR(gr.MARCA)) as EmpresaNome,
                        gr.REVENDA as NumeroRevenda,
                        gr.RAZAO_SOCIAL as NomeRevenda,
                        gr.RAZAO_SOCIAL as RazaoSocial,
                        gr.CNPJ,
                        gr.ENDERECO
                    FROM GER_REVENDA gr
                    LEFT JOIN GER_MARCA gm ON gm.MARCA = gr.MARCA
                    WHERE gr.EMPRESA = :empresa AND gr.REVENDA = :revenda";

                await using var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add("empresa", OracleDbType.Int32, empresaNumero, System.Data.ParameterDirection.Input);
                command.Parameters.Add("revenda", OracleDbType.Int32, revendaNumero, System.Data.ParameterDirection.Input);

                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new OracleRevendaDto
                    {
                        EmpresaNumero = GetInt(reader, "EmpresaNumero"),
                        EmpresaNome = GetString(reader, "EmpresaNome"),
                        NumeroRevenda = GetInt(reader, "NumeroRevenda"),
                        NomeRevenda = GetString(reader, "NomeRevenda"),
                        RazaoSocial = GetString(reader, "RazaoSocial"),
                        Cnpj = NormalizeCnpj(GetString(reader, "CNPJ")),
                        Endereco = GetString(reader, "Endereco")
                    };
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Erro ao buscar revenda do Oracle.", ex);
            }

            return null;
        }

        private void EnsureConnectionString()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string Oracle nao configurada para empresas e revendas.");
        }

        private static string GetOracleConnectionString(IConfiguration configuration)
        {
            var environment = configuration["Oracle:Environment"]?.Trim();
            var key = environment == "Production"
                ? "OracleConnectionProduction"
                : "OracleConnectionDve";

            var selectedConnection = configuration.GetConnectionString(key);
            if (!string.IsNullOrWhiteSpace(selectedConnection))
                return selectedConnection;

            var fallbackConnection = configuration.GetConnectionString("OracleConnection");
            return string.IsNullOrWhiteSpace(fallbackConnection) ? string.Empty : fallbackConnection;
        }

        private static string GetString(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
        }

        private static int GetInt(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static string NormalizeCnpj(string cnpj)
        {
            return new string(cnpj.Where(char.IsDigit).ToArray());
        }

        private async Task<bool> IsEmpresaAtivaAsync(int empresaNumero)
        {
            var config = await _context.EmpresaRevendaStatusConfig
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.EmpresaNumero == empresaNumero);
            return config?.Ativa ?? true;
        }

        private List<int> GetEmpresasPermitidas(string role)
        {
            var perfil = PerfisService.NormalizePerfilName(role);
            if (string.IsNullOrWhiteSpace(perfil))
                return new List<int>();

            return _context.PerfilSistema
                .AsNoTracking()
                .Where(p => p.Nome == perfil)
                .SelectMany(p => p.Empresas.Select(e => e.EmpresaNumero))
                .Distinct()
                .ToList();
        }

        private static string BuildConfigKey(int empresaNumero, int revendaNumero)
        {
            return $"{empresaNumero}:{revendaNumero}";
        }

        private static int BuildStableId(int empresaNumero, int revendaNumero)
        {
            return empresaNumero * 10000 + revendaNumero;
        }
    }
}
