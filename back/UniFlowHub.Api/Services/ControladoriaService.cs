using UniFlowHub.Api.Dtos.Controladoria;
using UniFlowHub.Api.Data;
using UniFlowHub.Api.Models;
using UniFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Data.Common;

namespace UniFlowHub.Api.Services
{
    public class ControladoriaService
    {
        private readonly AppDbContext _context;
        private readonly string _connectionString;

        // Edite esta consulta quando definir as tabelas/colunas reais do Oracle.
        // Mantenha os aliases abaixo para o frontend continuar funcionando sem ajustes.
        private const string ListGuiasIcmsSql = @"
            SELECT DISTINCT
                        FMC.EMPRESA || '-' || FMC.REVENDA || '-' || FMC.NUMERO_NOTA_FISCAL || '-' || FMC.SERIE_NOTA_FISCAL || '-' || FMC.TIPO_TRANSACAO || '-' || FMC.CONTADOR AS ID_GUIA,
                        
                        TO_CHAR(FMC.NUMERO_NOTA_FISCAL) AS DOCUMENTO,
                        TO_CHAR(FMC.EMPRESA) AS EMPRESA,
                        TO_CHAR(FMC.REVENDA) AS REVENDA,
                        TO_CHAR(FMC.NUMERO_NOTA_FISCAL) AS NUMERO_NOTA,
                        
                        FMC.TIPO_TRANSACAO AS TRANSACAO,

                        CASE
                            WHEN FCL.ENDERECO_COBRANCA = 2 THEN FCL.UF_COBRANCA
                            ELSE FCL.UF_ENTREGA
                        END AS UF,

                        FMC.TOT_NOTA_FISCAL - CASE
                            WHEN COALESCE(FMC.ICMSDESONERADO_DESCONTADONOTA, 'N') = 'S'
                                THEN COALESCE(FMC.ICMS_DESONERADO, 0)
                            ELSE 0
                        END AS VALOR,

                        COALESCE(FMC.VAL_ICMS_PARTIL_UF_DEST, 0) AS DIFAL,

                        COALESCE(FMC.VAL_DIFAL_FCP, 0)
                            + COALESCE(FMC.VAL_ICMS_COMB_POBREZA, 0)
                            + COALESCE(FMC.VAL_FCP_OUTROS, 0) AS FCP,

                        'Pendente' AS STATUS_PAGAMENTO,

                        'Consulta base em FAT_MOVIMENTO_CAPA. Ajuste filtros em ControladoriaService.cs se necessario.' AS OBSERVACOES

                    FROM FAT_MOVIMENTO_CAPA FMC

                    LEFT JOIN FAT_CLIENTE FCL 
                        ON FCL.CLIENTE = FMC.CLIENTE

                    INNER JOIN FAT_TIPO_TRANSACAO FTT 
                            ON FTT.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO

                    WHERE FTT.TIPO = 'S'
                    AND FMC.TIPO_TRANSACAO IN ('F21', 'P41', 'P21')
                    AND FMC.STATUS = 'F'
                    AND COALESCE(FMC.NFE_SITUACAO, ' ') <> 'D'
                    AND FMC.DTA_ENTRADA_SAIDA BETWEEN :DATA_INICIO AND :DATA_FIM

                    AND (:EMPRESA IS NULL OR TO_CHAR(FMC.EMPRESA) = :EMPRESA)
                    AND (:REVENDA IS NULL OR TO_CHAR(FMC.REVENDA) = :REVENDA)
                    AND (:TRANSACAO IS NULL OR FMC.TIPO_TRANSACAO = :TRANSACAO)
                    AND (:UF IS NULL OR (
                        CASE
                            WHEN FCL.ENDERECO_COBRANCA = 2 THEN FCL.UF_COBRANCA
                            ELSE FCL.UF_ENTREGA
                        END
                    ) = :UF)

                    AND (
                            COALESCE(FMC.VAL_ICMS_PARTIL_UF_DEST, 0) > 0
                            OR
                            (
                                COALESCE(FMC.VAL_DIFAL_FCP, 0)
                                + COALESCE(FMC.VAL_ICMS_COMB_POBREZA, 0)
                                + COALESCE(FMC.VAL_FCP_OUTROS, 0)
                            ) > 0
                        )

                    ORDER BY EMPRESA, REVENDA, NUMERO_NOTA";

        public ControladoriaService(IConfiguration configuration, AppDbContext context)
        {
            _context = context;
            _connectionString = GetOracleConnectionString(configuration);
        }

        public async Task<List<GuiaIcmsResponseDto>> ListGuiasIcmsAsync(string role, GuiaIcmsFilterDto filter)
        {
            EnsureCanAccess(role);
            EnsureConnectionString();

            var dataInicio = (filter.DataInicio ?? DateTime.Today.AddDays(-30)).Date;
            var dataFim = (filter.DataFim ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            if (dataInicio > dataFim)
                throw new InvalidOperationException("Data inicial nao pode ser maior que a data final.");

            var items = new List<GuiaIcmsResponseDto>();
            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandText = ListGuiasIcmsSql;
            command.CommandType = CommandType.Text;
            command.Parameters.Add("DATA_INICIO", OracleDbType.Date, dataInicio, ParameterDirection.Input);
            command.Parameters.Add("DATA_FIM", OracleDbType.Date, dataFim, ParameterDirection.Input);
            command.Parameters.Add("EMPRESA", OracleDbType.Varchar2, NormalizeFilter(filter.Empresa), ParameterDirection.Input);
            command.Parameters.Add("REVENDA", OracleDbType.Varchar2, NormalizeFilter(filter.Revenda), ParameterDirection.Input);
            command.Parameters.Add("TRANSACAO", OracleDbType.Varchar2, NormalizeTransactionFilter(filter.Transacao), ParameterDirection.Input);
            command.Parameters.Add("UF", OracleDbType.Varchar2, NormalizeUfFilter(filter.Uf), ParameterDirection.Input);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new GuiaIcmsResponseDto
                {
                    Id = GetString(reader, "ID_GUIA"),
                    Documento = GetString(reader, "DOCUMENTO"),
                    Empresa = GetString(reader, "EMPRESA"),
                    Revenda = GetString(reader, "REVENDA"),
                    NumeroNota = GetString(reader, "NUMERO_NOTA"),
                    Transacao = GetString(reader, "TRANSACAO"),
                    Cnpj = GetOptionalString(reader, "CNPJ"),
                    Competencia = GetOptionalString(reader, "COMPETENCIA"),
                    DataVencimento = GetOptionalDateTime(reader, "DATA_VENCIMENTO"),
                    DataPagamento = GetOptionalDateTime(reader, "DATA_PAGAMENTO"),
                    Valor = GetDecimal(reader, "VALOR"),
                    Difal = GetDecimal(reader, "DIFAL"),
                    Fcp = GetDecimal(reader, "FCP"),
                    Uf = GetString(reader, "UF"),
                    Status = NormalizeStatus(GetString(reader, "STATUS_PAGAMENTO")),
                    Observacoes = GetString(reader, "OBSERVACOES")
                });
            }

            ApplyPostgresPagamentoStatus(items);
            return items;
        }

        public async Task<GuiaIcmsResponseDto> UpdateGuiaIcmsPagamentoAsync(string id, GuiaIcmsPagamentoUpdateDto dto, string role, int userId)
        {
            EnsureCanAccess(role);

            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("Identificador da guia e obrigatorio.");

            var normalizedStatus = NormalizeStatus(dto.Status);
            if (normalizedStatus != "Pago" && normalizedStatus != "Pendente")
                throw new InvalidOperationException("Status invalido. Use Pago ou Pendente.");

            var guiaId = id.Trim();
            var pagamento = await _context.GuiaIcmsPagamento.FirstOrDefaultAsync(item => item.GuiaId == guiaId);
            if (normalizedStatus == "Pago")
            {
                if (pagamento is null)
                {
                    pagamento = new GuiaIcmsPagamento
                    {
                        GuiaId = guiaId,
                        Status = "Pago",
                        DataPagamento = DateTime.UtcNow,
                        DataAtualizacao = DateTime.UtcNow,
                        AtualizadoPorUserId = userId
                    };
                    _context.GuiaIcmsPagamento.Add(pagamento);
                }
                else
                {
                    pagamento.Status = "Pago";
                    pagamento.DataPagamento = DateTime.UtcNow;
                    pagamento.DataAtualizacao = DateTime.UtcNow;
                    pagamento.AtualizadoPorUserId = userId;
                }
            }
            else if (pagamento is not null)
            {
                _context.GuiaIcmsPagamento.Remove(pagamento);
            }

            await _context.SaveChangesAsync();

            return new GuiaIcmsResponseDto
            {
                Id = guiaId,
                Status = normalizedStatus,
                DataPagamento = normalizedStatus == "Pago" ? pagamento?.DataPagamento ?? DateTime.UtcNow : null
            };
        }

        public async Task<GuiaIcmsPagamentoLoteResponseDto> UpdateGuiaIcmsPagamentoLoteAsync(GuiaIcmsPagamentoLoteDto dto, string role, int userId)
        {
            EnsureCanAccess(role);

            var normalizedStatus = NormalizeStatus(dto.Status);
            if (normalizedStatus != "Pago")
                throw new InvalidOperationException("Baixa em lote permitida somente para marcar guias como pagas.");

            var guiaIds = dto.GuiaIds
                .Select(id => (id ?? string.Empty).Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (guiaIds.Count == 0)
                throw new InvalidOperationException("Nenhuma guia informada para baixa em lote.");

            var now = DateTime.UtcNow;
            var pagamentos = await _context.GuiaIcmsPagamento
                .Where(item => guiaIds.Contains(item.GuiaId))
                .ToDictionaryAsync(item => item.GuiaId, StringComparer.OrdinalIgnoreCase);

            foreach (var guiaId in guiaIds)
            {
                if (!pagamentos.TryGetValue(guiaId, out var pagamento))
                {
                    pagamento = new GuiaIcmsPagamento
                    {
                        GuiaId = guiaId,
                        Status = "Pago",
                        DataPagamento = now,
                        DataAtualizacao = now,
                        AtualizadoPorUserId = userId
                    };
                    _context.GuiaIcmsPagamento.Add(pagamento);
                    continue;
                }

                pagamento.Status = "Pago";
                pagamento.DataPagamento = now;
                pagamento.DataAtualizacao = now;
                pagamento.AtualizadoPorUserId = userId;
            }

            await _context.SaveChangesAsync();

            return new GuiaIcmsPagamentoLoteResponseDto
            {
                Atualizadas = guiaIds.Count,
                Status = "Pago",
                DataPagamento = now
            };
        }

        private void EnsureConnectionString()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string Oracle nao configurada para o ambiente selecionado.");
        }

        private void ApplyPostgresPagamentoStatus(List<GuiaIcmsResponseDto> items)
        {
            if (items.Count == 0)
                return;

            var ids = items.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var pagamentos = _context.GuiaIcmsPagamento
                .Where(item => ids.Contains(item.GuiaId))
                .ToDictionary(item => item.GuiaId, StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                if (pagamentos.TryGetValue(item.Id, out var pagamento) && NormalizeStatus(pagamento.Status) == "Pago")
                {
                    item.Status = "Pago";
                    item.DataPagamento = pagamento.DataPagamento;
                }
            }
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

        private static void EnsureCanAccess(string role)
        {
            if (!RoleScope.IsAdmin(role) && !RoleScope.IsTI(role) && !RoleScope.IsControladoria(role) && !RoleScope.IsDiretoria(role))
                throw new UnauthorizedAccessException("Acesso permitido somente para Admin, TI, Controladoria ou Diretoria.");
        }

        private static object NormalizeFilter(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? DBNull.Value : normalized;
        }

        private static object NormalizeTransactionFilter(string? value)
        {
            var normalized = value?.Trim().ToUpperInvariant();
            return normalized is "F21" or "P41" or "P21" ? normalized : DBNull.Value;
        }

        private static object NormalizeUfFilter(string? value)
        {
            var normalized = value?.Trim().ToUpperInvariant();
            return normalized is "AC" or "AL" or "AP" or "AM" or "BA" or "CE" or "DF" or "ES" or "GO" or "MA" or "MT" or "MS" or "MG" or "PA" or "PB" or "PR" or "PE" or "PI" or "RJ" or "RN" or "RS" or "RO" or "RR" or "SC" or "SP" or "SE" or "TO"
                ? normalized
                : DBNull.Value;
        }

        private static string NormalizeStatus(string value)
        {
            var normalized = value.Trim();
            return normalized.Equals("Pago", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Paga", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("S", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Sim", StringComparison.OrdinalIgnoreCase)
                ? "Pago"
                : "Pendente";
        }

        private static string GetString(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
        }

        private static string GetOptionalString(DbDataReader reader, string column)
        {
            return HasColumn(reader, column) ? GetString(reader, column) : string.Empty;
        }

        private static DateTime? GetOptionalDateTime(DbDataReader reader, string column)
        {
            return HasColumn(reader, column) ? GetNullableDateTime(reader, column) : null;
        }

        private static DateTime? GetNullableDateTime(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
        }

        private static decimal GetDecimal(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal));
        }

        private static bool HasColumn(DbDataReader reader, string column)
        {
            for (var index = 0; index < reader.FieldCount; index++)
            {
                if (reader.GetName(index).Equals(column, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
