using DRFlowHub.Api.Data;
using DRFlowHub.Api.Dtos.PecasBi;
using DRFlowHub.Api.Models;
using DRFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Data.Common;

namespace DRFlowHub.Api.Services
{
    public class PecasBiService
    {
        private readonly AppDbContext _context;
        private readonly string _connectionString;
        private sealed record PecaBiAccessScope(string? CpfVendedor, int? EmpresaNumero);

        // Ajuste os aliases caso o Oracle use nomes diferentes para margem, canal ou itens.
        private const string BaseCapaSql = @"
            WITH IMPOSTOS_ITEM AS (
                SELECT
                    EMPRESA,
                    REVENDA,
                    NUMERO_NOTA_FISCAL,
                    SERIE_NOTA_FISCAL,
                    TIPO_TRANSACAO,
                    CONTADOR,
                    SUM(
                        COALESCE(VAL_ICMS, 0)
                        + COALESCE(VAL_ICMS_RETIDO, 0)
                        + COALESCE(VAL_IPI, 0)
                        + COALESCE(VAL_PIS, 0)
                        + COALESCE(VAL_COFINS, 0)
                        + COALESCE(VAL_ICMS_PARTIL_UF_DEST, 0)
                        + COALESCE(VAL_ICMS_COMB_POBREZA, 0)
                        + COALESCE(VAL_FCP_ST, 0)
                        + COALESCE(VAL_FCP_OUTROS, 0)
                    ) AS IMPOSTOS
                FROM FAT_MOVIMENTO_ITEM
                GROUP BY EMPRESA, REVENDA, NUMERO_NOTA_FISCAL, SERIE_NOTA_FISCAL, TIPO_TRANSACAO, CONTADOR
            ),
            BASE AS (
                SELECT
                    FMC.EMPRESA,
                    FMC.REVENDA,
                    FMC.NUMERO_NOTA_FISCAL,
                    FMC.SERIE_NOTA_FISCAL,
                    FMC.TIPO_TRANSACAO,
                    FMC.CONTADOR,
                    FMC.DTA_ENTRADA_SAIDA,
                    FNV.VENDEDOR,
                    COALESCE(FV.NOME, 'Vendedor ' || TO_CHAR(FNV.VENDEDOR)) AS NOME_VENDEDOR,
                    REGEXP_REPLACE(COALESCE(TO_CHAR(FV.CPF), ''), '[^0-9]', '') AS CPF_VENDEDOR,
                    FMC.TIPO_TRANSACAO AS CANAL,
                    COALESCE(FMC.TOT_NOTA_FISCAL, 0) AS FATURAMENTO,
                    COALESCE(FMC.TOT_NOTA_FISCAL, 0) - COALESCE(IMP.IMPOSTOS, 0) AS MARGEM
                FROM FAT_MOVIMENTO_CAPA FMC
                LEFT JOIN IMPOSTOS_ITEM IMP
                  ON IMP.EMPRESA = FMC.EMPRESA
                 AND IMP.REVENDA = FMC.REVENDA
                 AND IMP.NUMERO_NOTA_FISCAL = FMC.NUMERO_NOTA_FISCAL
                 AND IMP.SERIE_NOTA_FISCAL = FMC.SERIE_NOTA_FISCAL
                 AND IMP.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                 AND IMP.CONTADOR = FMC.CONTADOR
                LEFT JOIN FAT_NOTAS_VENDEDOR FNV
                  ON FNV.EMPRESA = FMC.EMPRESA
                 AND FNV.REVENDA = FMC.REVENDA
                 AND FNV.NUMERO_NOTA_FISCAL = FMC.NUMERO_NOTA_FISCAL
                 AND FNV.SERIE_NOTA_FISCAL = FMC.SERIE_NOTA_FISCAL
                 AND FNV.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                 AND FNV.CONTADOR = FMC.CONTADOR
                LEFT JOIN FAT_VENDEDOR FV
                  ON FV.EMPRESA = FMC.EMPRESA
                 AND FV.VENDEDOR = FNV.VENDEDOR
                WHERE FMC.TIPO_TRANSACAO IN ('P21', 'P23', 'P41')
                  AND TO_CHAR(FMC.DEPARTAMENTO) = '3'
                  AND FMC.STATUS = 'F'
                  AND COALESCE(FMC.NFE_SITUACAO, ' ') <> 'D'
                  AND FMC.DTA_ENTRADA_SAIDA BETWEEN :DATA_INICIO AND :DATA_FIM
                  AND (:EMPRESA IS NULL OR TO_CHAR(FMC.EMPRESA) = :EMPRESA)
                  AND (:REVENDA IS NULL OR TO_CHAR(FMC.REVENDA) = :REVENDA)
                  AND (:CPF_VENDEDOR IS NULL OR REGEXP_REPLACE(COALESCE(TO_CHAR(FV.CPF), ''), '[^0-9]', '') = :CPF_VENDEDOR)
                  AND (:CANAL IS NULL OR FMC.TIPO_TRANSACAO = :CANAL)
            )";

        private const string VendasMensaisSql = BaseCapaSql + @"
            SELECT
                TO_CHAR(TRUNC(DTA_ENTRADA_SAIDA, 'MM'), 'MM/YYYY') AS MES,
                SUM(FATURAMENTO) AS FATURAMENTO,
                SUM(MARGEM) AS MARGEM,
                COUNT(*) AS QUANTIDADE
            FROM BASE
            GROUP BY TRUNC(DTA_ENTRADA_SAIDA, 'MM')
            ORDER BY TRUNC(DTA_ENTRADA_SAIDA, 'MM')";

        private const string CanaisSql = BaseCapaSql + @"
            SELECT CANAL, SUM(FATURAMENTO) AS FATURAMENTO
            FROM BASE
            GROUP BY CANAL
            ORDER BY FATURAMENTO DESC";

        private const string MetaVendedorSql = BaseCapaSql + @"
            SELECT COALESCE(SUM(FATURAMENTO), 0) AS FATURAMENTO
            FROM BASE";

        private const string VendedoresSql = BaseCapaSql + @"
            SELECT CPF_VENDEDOR, NOME_VENDEDOR, SUM(FATURAMENTO) AS FATURAMENTO, COUNT(*) AS PEDIDOS
            FROM BASE
            GROUP BY CPF_VENDEDOR, NOME_VENDEDOR
            ORDER BY FATURAMENTO DESC";

        private const string TopClientesSql = BaseCapaSql + @"
            SELECT *
            FROM (
                SELECT
                    TO_CHAR(FMC.CLIENTE) AS CODIGO,
                    COALESCE(FCL.NOME, 'Cliente ' || TO_CHAR(FMC.CLIENTE)) AS NOME,
                    SUM(B.FATURAMENTO) AS FATURAMENTO,
                    COUNT(*) AS NOTAS
                FROM BASE B
                INNER JOIN FAT_MOVIMENTO_CAPA FMC
                  ON FMC.EMPRESA = B.EMPRESA
                 AND FMC.REVENDA = B.REVENDA
                 AND FMC.NUMERO_NOTA_FISCAL = B.NUMERO_NOTA_FISCAL
                 AND FMC.SERIE_NOTA_FISCAL = B.SERIE_NOTA_FISCAL
                 AND FMC.TIPO_TRANSACAO = B.TIPO_TRANSACAO
                 AND FMC.CONTADOR = B.CONTADOR
                LEFT JOIN FAT_CLIENTE FCL
                  ON FCL.CLIENTE = FMC.CLIENTE
                GROUP BY FMC.CLIENTE, FCL.NOME
                ORDER BY FATURAMENTO DESC
            )
            WHERE ROWNUM <= 10";

        private const string TopPecasSql = @"
            SELECT *
            FROM (
                SELECT
                    TO_CHAR(FMI.ITEM_ESTOQUE) AS CODIGO,
                    COALESCE(PIE.DES_ITEM_ESTOQUE, 'Peca ' || TO_CHAR(FMI.ITEM_ESTOQUE)) AS NOME,
                    COALESCE(TO_CHAR(PIE.GRUPO), 'Pecas') AS CATEGORIA,
                    SUM(COALESCE(FMI.QUANTIDADE, 0)) AS QUANTIDADE,
                    SUM(COALESCE(FMI.VAL_TOTAL_REAL_ITEM, 0)) AS FATURAMENTO,
                    0 AS MARGEM_PERCENTUAL
                FROM FAT_MOVIMENTO_CAPA FMC
                INNER JOIN FAT_MOVIMENTO_ITEM FMI
                  ON FMI.EMPRESA = FMC.EMPRESA
                 AND FMI.REVENDA = FMC.REVENDA
                 AND FMI.NUMERO_NOTA_FISCAL = FMC.NUMERO_NOTA_FISCAL
                 AND FMI.SERIE_NOTA_FISCAL = FMC.SERIE_NOTA_FISCAL
                 AND FMI.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                 AND FMI.CONTADOR = FMC.CONTADOR
                LEFT JOIN FAT_NOTAS_VENDEDOR FNV
                  ON FNV.EMPRESA = FMC.EMPRESA
                 AND FNV.REVENDA = FMC.REVENDA
                 AND FNV.NUMERO_NOTA_FISCAL = FMC.NUMERO_NOTA_FISCAL
                 AND FNV.SERIE_NOTA_FISCAL = FMC.SERIE_NOTA_FISCAL
                 AND FNV.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                 AND FNV.CONTADOR = FMC.CONTADOR
                LEFT JOIN PEC_ITEM_ESTOQUE PIE
                  ON PIE.EMPRESA = FMI.EMPRESA
                 AND PIE.ITEM_ESTOQUE = FMI.ITEM_ESTOQUE
                LEFT JOIN FAT_VENDEDOR FV
                  ON FV.EMPRESA = FMC.EMPRESA
                 AND FV.VENDEDOR = FNV.VENDEDOR
                WHERE FMC.TIPO_TRANSACAO IN ('P21', 'P23', 'P41')
                  AND TO_CHAR(FMC.DEPARTAMENTO) = '3'
                  AND FMC.STATUS = 'F'
                  AND COALESCE(FMC.NFE_SITUACAO, ' ') <> 'D'
                  AND FMC.DTA_ENTRADA_SAIDA BETWEEN :DATA_INICIO AND :DATA_FIM
                  AND (:EMPRESA IS NULL OR TO_CHAR(FMC.EMPRESA) = :EMPRESA)
                  AND (:REVENDA IS NULL OR TO_CHAR(FMC.REVENDA) = :REVENDA)
                  AND (:CPF_VENDEDOR IS NULL OR REGEXP_REPLACE(COALESCE(TO_CHAR(FV.CPF), ''), '[^0-9]', '') = :CPF_VENDEDOR)
                  AND (:CANAL IS NULL OR FMC.TIPO_TRANSACAO = :CANAL)
                GROUP BY FMI.ITEM_ESTOQUE, PIE.DES_ITEM_ESTOQUE, PIE.GRUPO
                ORDER BY FATURAMENTO DESC
            )
            WHERE ROWNUM <= 10";

        public PecasBiService(IConfiguration configuration, AppDbContext context)
        {
            _context = context;
            _connectionString = GetOracleConnectionString(configuration);
        }

        public async Task<PecasBiResponseDto> LoadAsync(string role, int userId, PecasBiFilterDto filter)
        {
            await EnsureCanAccessAsync(role);
            EnsureConnectionString();

            var dataInicio = (filter.DataInicio ?? DateTime.Today.AddMonths(-5)).Date;
            var dataFim = (filter.DataFim ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            if (dataInicio > dataFim)
                throw new InvalidOperationException("Data inicial nao pode ser maior que a data final.");

            var accessScope = await GetAccessScopeAsync(role, userId);
            var cpfVendedor = accessScope.CpfVendedor;
            var empresa = NormalizeFilter(filter.Empresa);
            var revenda = NormalizeFilter(filter.Revenda);
            var canal = NormalizeFilter(filter.Canal);

            if (RoleScope.IsVendedorPecas(role))
            {
                empresa = DBNull.Value;
                revenda = DBNull.Value;
            }
            else if (RoleScope.IsGerentePecas(role))
            {
                if (!accessScope.EmpresaNumero.HasValue || accessScope.EmpresaNumero.Value <= 0)
                    throw new UnauthorizedAccessException("Empresa do gerente de pecas nao configurada no cadastro do usuario.");

                empresa = accessScope.EmpresaNumero.Value.ToString();
            }

            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var vendas = await LoadVendasMensaisAsync(connection, dataInicio, dataFim, empresa, revenda, cpfVendedor, canal);
            var canais = await LoadCanaisAsync(connection, dataInicio, dataFim, empresa, revenda, cpfVendedor, canal);
            var podeVerRankingVendedores = RoleScope.IsGerenteGeralPecas(role) || RoleScope.IsGerentePecas(role) || RoleScope.IsAdmin(role) || RoleScope.IsTI(role);
            var vendedores = podeVerRankingVendedores
                ? await LoadVendedoresAsync(connection, dataInicio, dataFim, empresa, revenda, canal)
                : new List<PecaVendedorDto>();
            var clientes = podeVerRankingVendedores
                ? await LoadTopClientesAsync(connection, dataInicio, dataFim, empresa, revenda, cpfVendedor, canal)
                : new List<PecaClienteDto>();
            var pecas = await LoadTopPecasAsync(connection, dataInicio, dataFim, empresa, revenda, cpfVendedor, canal);
            await ApplyMetasAsync(vendedores);
            var minhaMeta = await LoadMinhaMetaAsync(connection, cpfVendedor, canal);

            return new PecasBiResponseDto
            {
                AtualizadoEm = DateTime.UtcNow,
                PodeVerRankingVendedores = podeVerRankingVendedores,
                VendasMensais = vendas,
                Canais = canais,
                Vendedores = vendedores,
                Clientes = clientes,
                Pecas = pecas,
                MinhaMeta = minhaMeta,
                Categorias = new List<PecaCategoriaDto>()
            };
        }

        public async Task<PecaVendedorMetaDto> SaveMetaAsync(string role, int userId, PecaVendedorMetaDto dto)
        {
            if (!RoleScope.IsGerenteGeralPecas(role) && !RoleScope.IsGerentePecas(role) && !RoleScope.IsAdmin(role) && !RoleScope.IsTI(role))
                throw new UnauthorizedAccessException("Somente Gerente Geral de Pecas, Gerente de Pecas, Admin ou TI podem configurar metas de vendedores.");

            var cpf = OnlyDigits(dto.CpfVendedor);
            if (string.IsNullOrWhiteSpace(cpf))
                throw new InvalidOperationException("CPF do vendedor nao informado.");

            if (dto.ValorMeta < 0)
                throw new InvalidOperationException("A meta de vendas nao pode ser negativa.");

            if (!dto.DataInicio.HasValue || !dto.DataFim.HasValue)
                throw new InvalidOperationException("Informe a data inicial e final da meta.");

            var dataInicio = dto.DataInicio.Value.Date;
            var dataFim = dto.DataFim.Value.Date;
            if (dataInicio > dataFim)
                throw new InvalidOperationException("A data inicial da meta nao pode ser maior que a data final.");

            var meta = await _context.PecaVendedorMeta.FirstOrDefaultAsync(item => item.CpfVendedor == cpf);
            if (meta is null)
            {
                meta = new PecaVendedorMeta { CpfVendedor = cpf };
                _context.PecaVendedorMeta.Add(meta);
            }

            meta.NomeVendedor = dto.NomeVendedor?.Trim() ?? string.Empty;
            meta.ValorMeta = dto.ValorMeta;
            meta.DataInicio = dataInicio;
            meta.DataFim = dataFim;
            meta.DataAtualizacao = DateTime.UtcNow;
            meta.AtualizadoPorUserId = userId;

            await _context.SaveChangesAsync();

            return new PecaVendedorMetaDto
            {
                CpfVendedor = meta.CpfVendedor,
                NomeVendedor = meta.NomeVendedor,
                ValorMeta = meta.ValorMeta,
                DataInicio = meta.DataInicio,
                DataFim = meta.DataFim
            };
        }

        private async Task<PecaBiAccessScope> GetAccessScopeAsync(string role, int userId)
        {
            if (!RoleScope.IsVendedorPecas(role) && !RoleScope.IsGerentePecas(role))
                return new PecaBiAccessScope(null, null);

            var user = await _context.User
                .Include(user => user.Unidade)
                .ThenInclude(unidade => unidade!.EmpresaCadastro)
                .Where(user => user.Id == userId)
                .FirstOrDefaultAsync();

            if (user is null)
                throw new UnauthorizedAccessException("Usuario invalido.");

            if (RoleScope.IsVendedorPecas(role))
            {
                var cpf = OnlyDigits(user.Cpf);
                if (string.IsNullOrWhiteSpace(cpf))
                    throw new UnauthorizedAccessException("CPF do vendedor nao encontrado no cadastro do usuario.");

                return new PecaBiAccessScope(cpf, null);
            }

            var empresaNumero = user.Unidade?.EmpresaCadastro?.Numero;
            if (!empresaNumero.HasValue || empresaNumero.Value <= 0)
                throw new UnauthorizedAccessException("Empresa do gerente de pecas nao configurada no cadastro do usuario.");

            return new PecaBiAccessScope(null, empresaNumero);
        }

        private static async Task<List<PecaVendaMensalDto>> LoadVendasMensaisAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, object empresa, object revenda, string? cpfVendedor, object canal)
        {
            var items = new List<PecaVendaMensalDto>();
            await using var command = CreateCommand(connection, VendasMensaisSql, dataInicio, dataFim, empresa, revenda, cpfVendedor, canal);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new PecaVendaMensalDto
                {
                    Mes = GetString(reader, "MES"),
                    Faturamento = GetDecimal(reader, "FATURAMENTO"),
                    Margem = GetDecimal(reader, "MARGEM"),
                    Quantidade = GetInt(reader, "QUANTIDADE")
                });
            }

            return items;
        }

        private static async Task<List<PecaCanalDto>> LoadCanaisAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, object empresa, object revenda, string? cpfVendedor, object canal)
        {
            var items = new List<PecaCanalDto>();
            await using var command = CreateCommand(connection, CanaisSql, dataInicio, dataFim, empresa, revenda, cpfVendedor, canal);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new PecaCanalDto
                {
                    Nome = GetString(reader, "CANAL"),
                    Faturamento = GetDecimal(reader, "FATURAMENTO")
                });
            }

            return items;
        }

        private static async Task<List<PecaVendedorDto>> LoadVendedoresAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, object empresa, object revenda, object canal)
        {
            var items = new List<PecaVendedorDto>();
            await using var command = CreateCommand(connection, VendedoresSql, dataInicio, dataFim, empresa, revenda, null, canal);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new PecaVendedorDto
                {
                    Nome = GetString(reader, "NOME_VENDEDOR"),
                    CpfVendedor = GetString(reader, "CPF_VENDEDOR"),
                    Faturamento = GetDecimal(reader, "FATURAMENTO"),
                    Pedidos = GetInt(reader, "PEDIDOS"),
                    ConversaoPercentual = 0
                });
            }

            return items;
        }

        private static async Task<List<PecaClienteDto>> LoadTopClientesAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, object empresa, object revenda, string? cpfVendedor, object canal)
        {
            var items = new List<PecaClienteDto>();
            await using var command = CreateCommand(connection, TopClientesSql, dataInicio, dataFim, empresa, revenda, cpfVendedor, canal);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new PecaClienteDto
                {
                    Codigo = GetString(reader, "CODIGO"),
                    Nome = GetString(reader, "NOME"),
                    Faturamento = GetDecimal(reader, "FATURAMENTO"),
                    Notas = GetInt(reader, "NOTAS")
                });
            }

            return items;
        }

        private async Task ApplyMetasAsync(List<PecaVendedorDto> vendedores)
        {
            var cpfs = vendedores
                .Select(vendedor => OnlyDigits(vendedor.CpfVendedor))
                .Where(cpf => !string.IsNullOrWhiteSpace(cpf))
                .Distinct()
                .ToList();

            if (cpfs.Count == 0)
                return;

            var metas = await _context.PecaVendedorMeta
                .Where(meta => cpfs.Contains(meta.CpfVendedor))
                .ToDictionaryAsync(meta => meta.CpfVendedor);

            foreach (var vendedor in vendedores)
            {
                var cpf = OnlyDigits(vendedor.CpfVendedor);
                if (metas.TryGetValue(cpf, out var meta))
                {
                    vendedor.MetaVendas = meta.ValorMeta;
                    vendedor.MetaDataInicio = meta.DataInicio;
                    vendedor.MetaDataFim = meta.DataFim;
                }
            }
        }

        private async Task<PecaMetaResumoDto?> LoadMinhaMetaAsync(OracleConnection connection, string? cpfVendedor, object canal)
        {
            var cpf = OnlyDigits(cpfVendedor);
            if (string.IsNullOrWhiteSpace(cpf))
                return null;

            var meta = await _context.PecaVendedorMeta
                .Where(meta => meta.CpfVendedor == cpf)
                .FirstOrDefaultAsync();

            if (meta is null || !meta.DataInicio.HasValue || !meta.DataFim.HasValue)
                return null;

            var dataInicio = meta.DataInicio.Value.Date;
            var dataFim = meta.DataFim.Value.Date.AddDays(1).AddTicks(-1);
            var valorVendido = await LoadValorVendidoMetaAsync(connection, dataInicio, dataFim, cpf, canal);

            return new PecaMetaResumoDto
            {
                ValorVendido = valorVendido,
                ValorMeta = meta.ValorMeta,
                DataInicio = meta.DataInicio,
                DataFim = meta.DataFim
            };
        }

        private static async Task<decimal> LoadValorVendidoMetaAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, string cpfVendedor, object canal)
        {
            await using var command = CreateCommand(connection, MetaVendedorSql, dataInicio, dataFim, DBNull.Value, DBNull.Value, cpfVendedor, canal);
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? GetDecimal(reader, "FATURAMENTO") : 0;
        }

        private static async Task<List<PecaRankingDto>> LoadTopPecasAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, object empresa, object revenda, string? cpfVendedor, object canal)
        {
            var items = new List<PecaRankingDto>();
            await using var command = CreateCommand(connection, TopPecasSql, dataInicio, dataFim, empresa, revenda, cpfVendedor, canal);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new PecaRankingDto
                {
                    Codigo = GetString(reader, "CODIGO"),
                    Nome = GetString(reader, "NOME"),
                    Categoria = GetString(reader, "CATEGORIA"),
                    Quantidade = GetInt(reader, "QUANTIDADE"),
                    Faturamento = GetDecimal(reader, "FATURAMENTO"),
                    MargemPercentual = GetDecimal(reader, "MARGEM_PERCENTUAL"),
                    GiroDias = 0
                });
            }

            return items;
        }

        private static OracleCommand CreateCommand(OracleConnection connection, string sql, DateTime dataInicio, DateTime dataFim, object empresa, object revenda, string? cpfVendedor, object canal)
        {
            var command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.Parameters.Add("DATA_INICIO", OracleDbType.Date, dataInicio, ParameterDirection.Input);
            command.Parameters.Add("DATA_FIM", OracleDbType.Date, dataFim, ParameterDirection.Input);
            command.Parameters.Add("EMPRESA", OracleDbType.Varchar2, empresa, ParameterDirection.Input);
            command.Parameters.Add("REVENDA", OracleDbType.Varchar2, revenda, ParameterDirection.Input);
            command.Parameters.Add("CPF_VENDEDOR", OracleDbType.Varchar2, string.IsNullOrWhiteSpace(cpfVendedor) ? DBNull.Value : cpfVendedor, ParameterDirection.Input);
            command.Parameters.Add("CANAL", OracleDbType.Varchar2, canal, ParameterDirection.Input);
            return command;
        }

        private void EnsureConnectionString()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string Oracle nao configurada para o B.I de pecas.");
        }

        private async Task EnsureCanAccessAsync(string role)
        {
            if (!RoleScope.IsAdmin(role)
                && !RoleScope.IsTI(role)
                && !RoleScope.IsGerenteGeralPecas(role)
                && !RoleScope.IsGerentePecas(role)
                && !RoleScope.IsVendedorPecas(role)
                && !await HasPerfilAccessAsync(role, "vendas-pecas"))
            {
                throw new UnauthorizedAccessException("Acesso permitido somente para Gerente Geral de Pecas, Gerente de Pecas, Vendedor de Pecas, Admin ou TI.");
            }
        }

        private async Task<bool> HasPerfilAccessAsync(string role, string access)
        {
            var perfil = PerfisService.NormalizePerfilName(role);
            return await _context.PerfilSistema
                .AnyAsync(p => p.Nome == perfil && p.Acessos.Any(a => a.Chave == access));
        }

        private static object NormalizeFilter(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) || normalized.Equals("Todos", StringComparison.OrdinalIgnoreCase)
                ? DBNull.Value
                : normalized;
        }

        private static string OnlyDigits(string? value)
        {
            return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        }

        private static string GetOracleConnectionString(IConfiguration configuration)
        {
            var environment = configuration["Oracle:Environment"]?.Trim();
            var key = environment?.StartsWith("Prod", StringComparison.OrdinalIgnoreCase) == true
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

        private static decimal GetDecimal(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal));
        }

        private static int GetInt(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
        }
    }
}
