using UniFlowHub.Api.Dtos.Veiculos;
using UniFlowHub.Api.Security;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Data.Common;

namespace UniFlowHub.Api.Services
{
    public class VeiculosService
    {
        private readonly string _connectionString;

        private const string ListEstoqueSql = @"
            SELECT
                V.EMPRESA AS EMPRESA,
                V.REVENDA_ORIGEM AS REVENDA_ORIGEM,
                V.CHASSI AS CHASSI,
                TO_CHAR(V.VEICULO) AS VEICULO,
                TO_CHAR(V.MODELO) AS MODELO,
                M.DES_MODELO AS DES_MODELO,
                TO_CHAR(V.COR) AS COR,
                C.DES_COR AS DES_COR,
                COALESCE(V.RESERVADO, 'N') AS RESERVADO
            FROM VEI_VEICULO V
            LEFT JOIN VEI_COR C
              ON C.EMPRESA = V.EMPRESA
             AND C.COR = V.COR
            LEFT JOIN VEI_MODELO M
              ON M.EMPRESA = V.EMPRESA
             AND M.MODELO = V.MODELO
            WHERE V.EMPRESA = :EMPRESA
              AND V.SITUACAO = 'ES'
              AND V.NOVO_USADO = 'N'
              AND (:REVENDA_ORIGEM IS NULL OR V.REVENDA_ORIGEM = :REVENDA_ORIGEM)
              AND (
                :BUSCA IS NULL
                OR UPPER(V.CHASSI) LIKE '%' || :BUSCA || '%'
                OR UPPER(TO_CHAR(V.VEICULO)) LIKE '%' || :BUSCA || '%'
                OR UPPER(TO_CHAR(V.MODELO)) LIKE '%' || :BUSCA || '%'
                OR UPPER(M.DES_MODELO) LIKE '%' || :BUSCA || '%'
                OR UPPER(TO_CHAR(V.COR)) LIKE '%' || :BUSCA || '%'
                OR UPPER(C.DES_COR) LIKE '%' || :BUSCA || '%'
              )
              AND (:RESERVADO IS NULL OR COALESCE(V.RESERVADO, 'N') = :RESERVADO)
            ORDER BY V.VEICULO, V.CHASSI";

        private const string UpdateReservaSql = @"
            UPDATE VEI_VEICULO
               SET RESERVADO = :RESERVADO
             WHERE EMPRESA = :EMPRESA
               AND SITUACAO = 'ES'
               AND CHASSI = :CHASSI";

        public VeiculosService(IConfiguration configuration)
        {
            _connectionString = GetOracleConnectionString(configuration);
        }

        public async Task<List<VeiculoEstoqueResponseDto>> ListEstoqueAsync(string role, VeiculoEstoqueFilterDto filter)
        {
            EnsureCanView(role);
            EnsureConnectionString();
            var empresa = filter.Empresa ?? throw new InvalidOperationException("Empresa e obrigatoria para consultar o estoque.");
            EnsureEmpresaAllowed(role, empresa);

            var items = new List<VeiculoEstoqueResponseDto>();
            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandText = ListEstoqueSql;
            command.CommandType = CommandType.Text;
            command.Parameters.Add("EMPRESA", OracleDbType.Int32, empresa, ParameterDirection.Input);
            command.Parameters.Add("REVENDA_ORIGEM", OracleDbType.Int32, NormalizeRevendaFilter(filter.Revenda), ParameterDirection.Input);
            command.Parameters.Add("BUSCA", OracleDbType.Varchar2, NormalizeSearch(filter.Busca), ParameterDirection.Input);
            command.Parameters.Add("RESERVADO", OracleDbType.Varchar2, NormalizeReservedFilter(filter.Reservado), ParameterDirection.Input);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new VeiculoEstoqueResponseDto
                {
                    Empresa = GetInt(reader, "EMPRESA"),
                    Revenda = GetInt(reader, "REVENDA_ORIGEM"),
                    Chassi = GetString(reader, "CHASSI"),
                    CodigoVeiculo = GetString(reader, "VEICULO"),
                    Modelo = GetString(reader, "MODELO"),
                    DescricaoModelo = GetString(reader, "DES_MODELO"),
                    Cor = GetString(reader, "COR"),
                    DescricaoCor = GetString(reader, "DES_COR"),
                    Reservado = NormalizeReserved(GetString(reader, "RESERVADO")),
                    OrigemReserva = NormalizeReserved(GetString(reader, "RESERVADO")) ? "Oracle" : string.Empty
                });
            }

            return items;
        }

        public async Task<VeiculoEstoqueResponseDto> UpdateReservaAsync(string chassi, VeiculoReservaUpdateDto dto, string role, int userId)
        {
            if (!RoleScope.IsQualidadeNissan(role) && !RoleScope.IsAdmin(role) && !RoleScope.IsTI(role))
                throw new UnauthorizedAccessException("Apenas Qualidade Nissan, TI ou Admin pode alterar reservas de estoque.");

            var normalizedChassi = chassi.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalizedChassi))
                throw new InvalidOperationException("Chassi e obrigatorio.");

            if (dto.Empresa <= 0)
                throw new InvalidOperationException("Empresa e obrigatoria para alterar a reserva.");

            EnsureEmpresaAllowed(role, dto.Empresa);

            EnsureConnectionString();
            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandText = UpdateReservaSql;
            command.CommandType = CommandType.Text;
            command.Parameters.Add("RESERVADO", OracleDbType.Varchar2, dto.Reservado ? "S" : "N", ParameterDirection.Input);
            command.Parameters.Add("EMPRESA", OracleDbType.Int32, dto.Empresa, ParameterDirection.Input);
            command.Parameters.Add("CHASSI", OracleDbType.Varchar2, normalizedChassi, ParameterDirection.Input);

            var affected = await command.ExecuteNonQueryAsync();
            if (affected == 0)
                throw new InvalidOperationException("Veiculo nao encontrado no estoque da empresa selecionada.");

            return new VeiculoEstoqueResponseDto
            {
                Empresa = dto.Empresa,
                Revenda = 0,
                Chassi = normalizedChassi,
                CodigoVeiculo = string.Empty,
                Modelo = string.Empty,
                DescricaoModelo = string.Empty,
                Cor = string.Empty,
                DescricaoCor = string.Empty,
                Reservado = dto.Reservado,
                OrigemReserva = dto.Reservado ? "Oracle" : string.Empty
            };
        }

        private static void EnsureCanView(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                throw new UnauthorizedAccessException("Usuario invalido.");
        }

        private static void EnsureEmpresaAllowed(string role, int empresa)
        {
            if (RoleScope.IsQualidadeNissan(role) && empresa != 2)
                throw new UnauthorizedAccessException("Qualidade Nissan pode acessar apenas a empresa 2.");
        }

        private void EnsureConnectionString()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string Oracle nao configurada para o estoque.");
        }

        private static object NormalizeSearch(string? value)
        {
            var normalized = value?.Trim().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(normalized) ? DBNull.Value : normalized;
        }

        private static object NormalizeReservedFilter(string? value)
        {
            var normalized = value?.Trim().ToUpperInvariant();
            return normalized is "S" or "N" ? normalized : DBNull.Value;
        }

        private static object NormalizeRevendaFilter(int? value)
        {
            var normalized = value.GetValueOrDefault();
            return normalized > 0 ? normalized : DBNull.Value;
        }

        private static bool NormalizeReserved(string value)
        {
            var normalized = value.Trim();
            return normalized.Equals("S", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("SIM", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("RESERVADO", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("1", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
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
    }
}
