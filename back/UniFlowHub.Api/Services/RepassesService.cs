using System.Data;
using System.Data.Common;
using UniFlowHub.Api.Dtos.Repasses;
using UniFlowHub.Api.Security;
using Oracle.ManagedDataAccess.Client;

namespace UniFlowHub.Api.Services
{
    public class RepassesService
    {
        private readonly string _connectionString;
        private static readonly Dictionary<int, decimal> LimitesAutorizados = new()
        {
            [1] = 4500000m,
            [2] = 4500000m,
            [5] = 2500000m,
            [6] = 5500000m,
            [7] = 800000m,
            [8] = 150000m,
            [9] = 300000m,
            [10] = 300000m,
        };

        private static readonly Dictionary<int, string> NomesEmpresas = new()
        {
            [1] = "Renault",
            [2] = "Nissan",
            [5] = "GM",
            [6] = "DFSUL",
            [7] = "DRSUL Peugeot/Citroen",
            [8] = "Bajaj",
            [9] = "Geely",
            [10] = "MG",
        };

        private const string DashboardSql = @"
            SELECT
                VEI.EMPRESA AS EMPRESA,
                VEI.REVENDA_ORIGEM AS REVENDA,
                TO_CHAR(VEI.EMPRESA) AS NOME_EMPRESA,
                REV.NOME_FANTASIA AS NOME_REVENDA,
                MOD.DES_MODELO AS MODELO,
                FIC.PLACA AS PLACA,
                VEI.VAL_CUSTO_CONTABIL AS CUSTO_CONTABIL,
                VEI.SITUACAO AS SITUACAO,
                CAST(
                    CASE
                        WHEN VEI.DTA_VENDA IS NULL THEN
                            CASE PAR.DTA_CALCULO_ESTOQUE
                                WHEN '1' THEN NVL(:DATA_REFERENCIA, CURRENT_DATE) - CAST(VEI.DTA_INICIO_CORRECAO AS DATE)
                                WHEN '2' THEN NVL(:DATA_REFERENCIA, CURRENT_DATE) - CAST(VEI.DTA_FAT_FABRICA AS DATE)
                                ELSE NVL(:DATA_REFERENCIA, CURRENT_DATE) - CAST(VEI.DTA_ENTRADA AS DATE)
                            END
                        ELSE
                            CASE PAR.DTA_CALCULO_ESTOQUE
                                WHEN '1' THEN NVL(:DATA_REFERENCIA, VEI.DTA_VENDA) - VEI.DTA_INICIO_CORRECAO
                                WHEN '2' THEN NVL(:DATA_REFERENCIA, VEI.DTA_VENDA) - VEI.DTA_FAT_FABRICA
                                ELSE NVL(:DATA_REFERENCIA, VEI.DTA_VENDA) - VEI.DTA_ENTRADA
                            END
                    END AS INTEGER
                ) AS DIAS_ESTOQUE
            FROM VEI_VEICULO VEI
            INNER JOIN OFI_FICHA_SEGUIMENTO FIC ON VEI.CHASSI = FIC.CHASSI
            INNER JOIN VEI_MODELO MOD ON VEI.EMPRESA = MOD.EMPRESA AND VEI.MODELO = MOD.MODELO
            INNER JOIN VEI_FAMILIA FAM ON MOD.EMPRESA = FAM.EMPRESA AND MOD.FAMILIA = FAM.FAMILIA
            INNER JOIN VEI_PARAMETRO PAR ON VEI.EMPRESA = PAR.EMPRESA AND VEI.REVENDA_ORIGEM = PAR.REVENDA
            INNER JOIN GER_REVENDA REV ON VEI.EMPRESA = REV.EMPRESA AND VEI.REVENDA_ORIGEM = REV.REVENDA
            WHERE (:EMPRESA IS NULL OR VEI.EMPRESA = :EMPRESA)
              AND VEI.EMPRESA IN (1, 2, 5, 6, 7, 8, 9, 10)
              AND (:REVENDA IS NULL OR VEI.REVENDA_ORIGEM = :REVENDA)
              AND VEI.SITUACAO IN (SELECT SITUACAO FROM VEI_SITUACAO WHERE EMPRESA = VEI.EMPRESA AND LOCALIZACAO IN ('E','F'))
              AND VEI.DEPARTAMENTO IN (1, 2)
              AND VEI.SITUACAO IN ('ES', 'IE', 'RE', 'SD', 'ST', 'TF')
              AND VEI.NOVO_USADO = 'U'
              AND (
                    :DATA_REFERENCIA IS NULL
                    OR (
                        CAST(NVL(VEI.DTA_ENTRADA, VEI.DTA_FAT_FABRICA) AS DATE) <= :DATA_REFERENCIA
                        AND (VEI.DTA_VENDA IS NULL OR CAST(VEI.DTA_VENDA AS DATE) > :DATA_REFERENCIA)
                    )
                  )
            ORDER BY DIAS_ESTOQUE DESC, VEI.EMPRESA, VEI.REVENDA_ORIGEM, MOD.DES_MODELO";

        public RepassesService(IConfiguration configuration)
        {
            _connectionString = GetOracleConnectionString(configuration);
        }

        public async Task<RepasseDashboardDto> GetDashboardAsync(string role, IEnumerable<string> acessos, RepasseDashboardFilterDto filter)
        {
            EnsureCanView(role, acessos);
            EnsureConnectionString();

            if (filter.Empresa.HasValue)
                EnsureEmpresaAllowed(role, filter.Empresa.Value);

            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var dataFim = filter.DataFim?.Date ?? filter.DataInicio?.Date;
            var dataInicio = filter.DataInicio?.Date;
            var veiculos = await LoadVeiculosAsync(connection, filter, dataFim);
            var veiculosInicio = dataInicio.HasValue
                ? await LoadVeiculosAsync(connection, filter, dataInicio)
                : veiculos;

            return new RepasseDashboardDto
            {
                Veiculos = veiculos,
                TopDiasEstoque = veiculos.OrderByDescending(item => item.DiasEstoque).Take(5).ToList(),
                Resumos = BuildResumos(veiculosInicio, veiculos),
            };
        }

        private static async Task<List<RepasseVeiculoDto>> LoadVeiculosAsync(OracleConnection connection, RepasseDashboardFilterDto filter, DateTime? dataReferencia)
        {
            var veiculos = new List<RepasseVeiculoDto>();

            await using var command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandText = DashboardSql;
            command.CommandType = CommandType.Text;
            command.Parameters.Add("EMPRESA", OracleDbType.Int32, NormalizeIntFilter(filter.Empresa), ParameterDirection.Input);
            command.Parameters.Add("REVENDA", OracleDbType.Int32, NormalizeIntFilter(filter.Revenda), ParameterDirection.Input);
            command.Parameters.Add("DATA_REFERENCIA", OracleDbType.Date, NormalizeDateFilter(dataReferencia), ParameterDirection.Input);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var empresa = GetInt(reader, "EMPRESA");
                veiculos.Add(new RepasseVeiculoDto
                {
                    Empresa = empresa,
                    Revenda = GetInt(reader, "REVENDA"),
                    NomeEmpresa = GetEmpresaNome(empresa, GetString(reader, "NOME_EMPRESA")),
                    NomeRevenda = GetString(reader, "NOME_REVENDA"),
                    Modelo = GetString(reader, "MODELO"),
                    Placa = GetString(reader, "PLACA"),
                    CustoContabil = GetDecimal(reader, "CUSTO_CONTABIL"),
                    Situacao = GetString(reader, "SITUACAO"),
                    DiasEstoque = GetInt(reader, "DIAS_ESTOQUE"),
                });
            }

            return veiculos;
        }

        private static List<RepasseResumoEmpresaDto> BuildResumos(List<RepasseVeiculoDto> veiculosInicio, List<RepasseVeiculoDto> veiculosFim)
        {
            var empresas = veiculosInicio.Select(item => item.Empresa)
                .Concat(veiculosFim.Select(item => item.Empresa))
                .Distinct()
                .OrderBy(item => item)
                .ToList();

            return empresas.Select(empresa =>
            {
                var inicio = veiculosInicio.Where(item => item.Empresa == empresa).ToList();
                var fim = veiculosFim.Where(item => item.Empresa == empresa).ToList();
                var custoPara = fim.Sum(item => item.CustoContabil);
                var volumePara = fim.Count;
                var limite = LimitesAutorizados.GetValueOrDefault(empresa, 0);

                return new RepasseResumoEmpresaDto
                {
                    Empresa = empresa,
                    NomeEmpresa = GetEmpresaNome(empresa, fim.FirstOrDefault()?.NomeEmpresa ?? inicio.FirstOrDefault()?.NomeEmpresa ?? string.Empty),
                    VolumeDe = inicio.Count,
                    VolumePara = volumePara,
                    CustoDe = inicio.Sum(item => item.CustoContabil),
                    CustoPara = custoPara,
                    TicketMedio = volumePara > 0 ? custoPara / volumePara : 0,
                    MediaGiroEstoque = volumePara > 0 ? Convert.ToInt32(Math.Round(fim.Average(item => item.DiasEstoque), MidpointRounding.AwayFromZero)) : 0,
                    Distorcao = limite > 0 ? custoPara - limite : custoPara,
                    LimiteAutorizado = limite,
                };
            }).ToList();
        }

        private static void EnsureCanView(string role, IEnumerable<string> acessos)
        {
            if (string.IsNullOrWhiteSpace(role))
                throw new UnauthorizedAccessException("Usuario invalido.");

            if (RoleScope.IsAdmin(role) || RoleScope.IsTI(role))
                return;

            if (!acessos.Any(acesso => string.Equals(acesso, "veiculos-repasses", StringComparison.OrdinalIgnoreCase)))
                throw new UnauthorizedAccessException("Perfil sem acesso aos repasses de veiculos.");
        }

        private static void EnsureEmpresaAllowed(string role, int empresa)
        {
            if (RoleScope.IsQualidadeNissan(role) && empresa != 2)
                throw new UnauthorizedAccessException("Qualidade Nissan pode acessar apenas a empresa 2.");
        }

        private void EnsureConnectionString()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string Oracle nao configurada para repasses.");
        }

        private static object NormalizeIntFilter(int? value)
        {
            var normalized = value.GetValueOrDefault();
            return normalized > 0 ? normalized : DBNull.Value;
        }

        private static object NormalizeDateFilter(DateTime? value)
        {
            return value.HasValue ? value.Value.Date : DBNull.Value;
        }

        private static string GetEmpresaNome(int empresa, string fallback)
        {
            if (NomesEmpresas.TryGetValue(empresa, out var nome))
                return nome;

            return string.IsNullOrWhiteSpace(fallback) ? empresa.ToString() : fallback;
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

        private static decimal GetDecimal(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal));
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
