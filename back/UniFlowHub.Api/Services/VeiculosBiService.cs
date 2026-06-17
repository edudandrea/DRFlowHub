using System.Data;
using System.Data.Common;
using System.Globalization;
using UniFlowHub.Api.Data;
using UniFlowHub.Api.Dtos.VeiculosBi;
using UniFlowHub.Api.Security;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace UniFlowHub.Api.Services
{
    public class VeiculosBiService
    {
        private readonly AppDbContext _context;
        private readonly string _connectionString;

        private const string AcessoriosSql = @"
            SELECT *
            FROM (
                SELECT
                    COALESCE(TO_CHAR(PIE.ITEM_ESTOQUE_PUB), TO_CHAR(FMI.ITEM_ESTOQUE)) AS CODIGO,
                    COALESCE(PIE.DES_ITEM_ESTOQUE, 'Acessorio ' || TO_CHAR(FMI.ITEM_ESTOQUE)) AS NOME,
                    COALESCE(TO_CHAR(PIE.GRUPO), 'Acessorios') AS CATEGORIA,
                    SUM(CASE WHEN TT.TIPO = 'E' THEN -1 ELSE 1 END * COALESCE(FMI.QUANTIDADE, 0)) AS QUANTIDADE,
                    SUM(CASE WHEN TT.TIPO = 'E' THEN -1 ELSE 1 END * (
                        COALESCE(FMI.VAL_TOTAL_REAL_ITEM, 0)
                        - (COALESCE(FMI.VAL_DESCONTO, 0) - COALESCE(FMI.VAL_DESCONTO_FRANQUIA, 0))
                        + COALESCE(FMI.VAL_FRETE, 0)
                    )) AS FATURAMENTO,
                    SUM(CASE WHEN TT.TIPO = 'E' THEN -1 ELSE 1 END * (
                        COALESCE(FMI.VAL_TOTAL_REAL_ITEM, 0)
                        - (COALESCE(FMI.VAL_DESCONTO, 0) - COALESCE(FMI.VAL_DESCONTO_FRANQUIA, 0))
                        + COALESCE(FMI.VAL_FRETE, 0)
                        - COALESCE(FMI.VAL_CUSTO_MEDIO, 0)
                    )) AS RENTABILIDADE
                FROM FAT_MOVIMENTO_CAPA FMC
                INNER JOIN FAT_MOVIMENTO_ITEM FMI
                   ON FMI.EMPRESA = FMC.EMPRESA
                  AND FMI.REVENDA = FMC.REVENDA
                  AND FMI.NUMERO_NOTA_FISCAL = FMC.NUMERO_NOTA_FISCAL
                  AND FMI.SERIE_NOTA_FISCAL = FMC.SERIE_NOTA_FISCAL
                  AND FMI.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                  AND FMI.CONTADOR = FMC.CONTADOR
                INNER JOIN FAT_TIPO_TRANSACAO TT
                   ON TT.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                LEFT JOIN PEC_ITEM_ESTOQUE PIE
                   ON PIE.EMPRESA = FMI.EMPRESA
                  AND PIE.ITEM_ESTOQUE = FMI.ITEM_ESTOQUE
                WHERE TO_CHAR(FMC.DEPARTAMENTO) = '7'
                  AND COALESCE(FMC.NFE_SITUACAO, ' ') <> 'D'
                  AND FMC.DTA_ENTRADA_SAIDA BETWEEN :DATA_INICIO AND :DATA_FIM
                  AND (:EMPRESA IS NULL OR INSTR(',' || :EMPRESA || ',', ',' || TO_CHAR(FMC.EMPRESA) || ',') > 0)
                  AND (:REVENDA IS NULL OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(FMC.REVENDA) || ',') > 0)
                GROUP BY FMI.ITEM_ESTOQUE, PIE.ITEM_ESTOQUE_PUB, PIE.DES_ITEM_ESTOQUE, PIE.GRUPO
                ORDER BY FATURAMENTO DESC
            )
            WHERE ROWNUM <= 10";

        private const string BaseVeiculosVendidosFromSql = @"
                FROM VEI_VEICULO VEI
                INNER JOIN OFI_FICHA_SEGUIMENTO FIC ON VEI.CHASSI = FIC.CHASSI
                INNER JOIN VEI_MODELO MOD ON VEI.EMPRESA = MOD.EMPRESA AND VEI.MODELO = MOD.MODELO
                INNER JOIN VEI_FAMILIA FAM ON MOD.EMPRESA = FAM.EMPRESA AND MOD.FAMILIA = FAM.FAMILIA
                INNER JOIN (
                    SELECT EMPRESA, REVENDA, NOME_FANTASIA, MARCA
                    FROM GER_REVENDA
                ) REV ON VEI.EMPRESA = REV.EMPRESA AND VEI.REVENDA_ORIGEM = REV.REVENDA
                WHERE VEI.SITUACAO IN (
                    SELECT SITUACAO
                    FROM VEI_SITUACAO
                    WHERE EMPRESA = VEI.EMPRESA
                      AND LOCALIZACAO = 'V'
                )
                  AND VEI.DTA_VENDA BETWEEN :DATA_INICIO AND :DATA_FIM
                  AND (:EMPRESA IS NULL OR INSTR(',' || :EMPRESA || ',', ',' || TO_CHAR(VEI.EMPRESA) || ',') > 0)
                  AND (:REVENDA IS NULL OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(VEI.REVENDA_ORIGEM) || ',') > 0)";

        private const string ValorVendaExpression = @"
            COALESCE(
                (
                    SELECT FMCS.TOT_NOTA_FISCAL - COALESCE(FMCS.VALDESCONTO, 0)
                    FROM FAT_MOVIMENTO_CAPA FMCS
                    WHERE VEI.EMPRESA_NFSAIDA = FMCS.EMPRESA
                      AND VEI.REVENDA_NFSAIDA = FMCS.REVENDA
                      AND VEI.NUMERO_NOTA_NFSAIDA = FMCS.NUMERO_NOTA_FISCAL
                      AND VEI.SERIE_NOTA_FISCAL_NFSAIDA = FMCS.SERIE_NOTA_FISCAL
                      AND VEI.TIPO_TRANSACAO_NFSAIDA = FMCS.TIPO_TRANSACAO
                      AND VEI.CONTADOR_NFSAIDA = FMCS.CONTADOR
                ),
                VEI.VAL_PRESENTE_VENDA,
                VEI.PRECO_CONCESSIONARIA,
                0
            )";

        private const string VendasFiliaisSql = @"
            SELECT
                VEI.EMPRESA AS EMPRESA_NUMERO,
                MAX('Empresa ' || TO_CHAR(VEI.EMPRESA)) AS EMPRESA_NOME,
                VEI.REVENDA_ORIGEM AS REVENDA_NUMERO,
                MAX(REV.NOME_FANTASIA) AS FILIAL,
                SUM(CASE WHEN VEI.NOVO_USADO = 'N' AND COALESCE(VEI.TIPO_VENDA, ' ') NOT IN ('F', 'D') THEN 1 ELSE 0 END) AS FATURADOS_NOVOS,
                SUM(CASE WHEN VEI.NOVO_USADO = 'N' AND COALESCE(VEI.TIPO_VENDA, ' ') IN ('F', 'D') THEN 1 ELSE 0 END) AS FATURADOS_DIRETA,
                SUM(CASE WHEN VEI.NOVO_USADO <> 'N' THEN 1 ELSE 0 END) AS SEMINOVOS,
                SUM(" + ValorVendaExpression + @") AS FATURAMENTO,
                SUM(" + ValorVendaExpression + @" - COALESCE(VEI.VAL_CUSTO_CONTABIL, VEI.VAL_COMPRA, 0)) AS MARGEM
            " + BaseVeiculosVendidosFromSql + @"
            GROUP BY VEI.EMPRESA, VEI.REVENDA_ORIGEM
            ORDER BY VEI.EMPRESA, VEI.REVENDA_ORIGEM";

        private const string VendasDiariasSql = @"
            SELECT
                TRUNC(VEI.DTA_VENDA) AS DATA_VENDA,
                SUM(CASE WHEN VEI.NOVO_USADO = 'N' AND COALESCE(VEI.TIPO_VENDA, ' ') NOT IN ('F', 'D') THEN 1 ELSE 0 END) AS NOVOS,
                SUM(CASE WHEN VEI.NOVO_USADO = 'N' AND COALESCE(VEI.TIPO_VENDA, ' ') IN ('F', 'D') THEN 1 ELSE 0 END) AS VENDA_DIRETA,
                SUM(CASE WHEN VEI.NOVO_USADO <> 'N' THEN 1 ELSE 0 END) AS SEMINOVOS
            " + BaseVeiculosVendidosFromSql + @"
            GROUP BY TRUNC(VEI.DTA_VENDA)
            ORDER BY TRUNC(VEI.DTA_VENDA)";

        private const string ModelosSql = @"
            SELECT *
            FROM (
                SELECT
                    MAX(MOD.DES_MODELO) AS MODELO,
                    MAX(FAM.DES_FAMILIA) AS FAMILIA,
                    COUNT(*) AS UNIDADES,
                    SUM(" + ValorVendaExpression + @") AS FATURAMENTO,
                    SUM(" + ValorVendaExpression + @" - COALESCE(VEI.VAL_CUSTO_CONTABIL, VEI.VAL_COMPRA, 0)) AS MARGEM
                " + BaseVeiculosVendidosFromSql + @"
                GROUP BY MOD.MODELO
                ORDER BY UNIDADES DESC
            )
            WHERE ROWNUM <= 12";

        private const string VendedoresSql = @"
            SELECT *
            FROM (
                SELECT
                    COALESCE(VEN_NOTA.NOME, VEN_VD.NOME, 'Sem vendedor') AS VENDEDOR,
                    MAX(REV.NOME_FANTASIA) AS FILIAL,
                    COUNT(*) AS REALIZADO,
                    SUM(" + ValorVendaExpression + @") AS FATURAMENTO
                FROM VEI_VEICULO VEI
                INNER JOIN OFI_FICHA_SEGUIMENTO FIC ON VEI.CHASSI = FIC.CHASSI
                INNER JOIN VEI_MODELO MOD ON VEI.EMPRESA = MOD.EMPRESA AND VEI.MODELO = MOD.MODELO
                INNER JOIN VEI_FAMILIA FAM ON MOD.EMPRESA = FAM.EMPRESA AND MOD.FAMILIA = FAM.FAMILIA
                INNER JOIN (
                    SELECT EMPRESA, REVENDA, NOME_FANTASIA, MARCA
                    FROM GER_REVENDA
                ) REV ON VEI.EMPRESA = REV.EMPRESA AND VEI.REVENDA_ORIGEM = REV.REVENDA
                LEFT JOIN FAT_NOTAS_VENDEDOR NOTA
                   ON NOTA.EMPRESA = VEI.EMPRESA_NFSAIDA
                  AND NOTA.REVENDA = VEI.REVENDA_NFSAIDA
                  AND NOTA.NUMERO_NOTA_FISCAL = VEI.NUMERO_NOTA_NFSAIDA
                  AND NOTA.SERIE_NOTA_FISCAL = VEI.SERIE_NOTA_FISCAL_NFSAIDA
                  AND NOTA.TIPO_TRANSACAO = VEI.TIPO_TRANSACAO_NFSAIDA
                  AND NOTA.CONTADOR = VEI.CONTADOR_NFSAIDA
                  AND (NOTA.TIPO_VENDEDOR = 'N' OR NOTA.TIPO_VENDEDOR IS NULL)
                LEFT JOIN FAT_VENDEDOR VEN_NOTA
                   ON VEN_NOTA.EMPRESA = NOTA.EMPRESA
                  AND VEN_NOTA.REVENDA = NOTA.REVENDA
                  AND VEN_NOTA.VENDEDOR = NOTA.VENDEDOR
                LEFT JOIN FAT_VENDEDOR VEN_VD
                   ON VEN_VD.EMPRESA = VEI.EMPRESA_VENDEDOR_VD
                  AND VEN_VD.REVENDA = VEI.REVENDA_VENDEDOR_VD
                  AND VEN_VD.VENDEDOR = VEI.VENDEDOR_VD
                WHERE VEI.SITUACAO IN (
                    SELECT SITUACAO
                    FROM VEI_SITUACAO
                    WHERE EMPRESA = VEI.EMPRESA
                      AND LOCALIZACAO = 'V'
                )
                  AND VEI.DTA_VENDA BETWEEN :DATA_INICIO AND :DATA_FIM
                  AND (:EMPRESA IS NULL OR INSTR(',' || :EMPRESA || ',', ',' || TO_CHAR(VEI.EMPRESA) || ',') > 0)
                  AND (:REVENDA IS NULL OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(VEI.REVENDA_ORIGEM) || ',') > 0)
                GROUP BY COALESCE(VEN_NOTA.NOME, VEN_VD.NOME, 'Sem vendedor')
                ORDER BY REALIZADO DESC
            )
            WHERE ROWNUM <= 12";

        private const string RetornoFiBaseFromSql = @"
                FROM CAC_CONTATO CAC
                INNER JOIN VEI_PROPOSTA VP
                   ON VP.EMPRESA = CAC.EMPRESA
                  AND VP.REVENDA = CAC.REVENDA
                  AND VP.CONTATO = CAC.CONTATO
                INNER JOIN VEI_NEGOCIACAO NEG
                   ON VP.EMPRESA = NEG.EMPRESA
                  AND VP.REVENDA = NEG.REVENDA
                  AND VP.PROPOSTA = NEG.PROPOSTA
                  AND VP.NEGOCIACAO_FINAL = NEG.NEGOCIACAO
                INNER JOIN FAT_VENDEDOR VEN
                   ON VP.EMPRESA = VEN.EMPRESA
                  AND VP.REVENDA = VEN.REVENDA
                  AND VP.VENDEDOR = VEN.VENDEDOR
                INNER JOIN VEI_VEICULO VEI
                   ON VP.EMPRESA = VEI.EMPRESA
                  AND VP.VEICULO = VEI.VEICULO
                LEFT JOIN VEI_RETORNO VR
                   ON VR.EMPRESA = VP.EMPRESA
                  AND VR.REVENDA = VP.REVENDA
                  AND VR.PROPOSTA = VP.PROPOSTA
                LEFT JOIN FAT_CLIENTE FIN
                   ON VR.CLIENTE = FIN.CLIENTE
                LEFT JOIN FAT_CLIENTE FNE
                   ON NEG.FINANCEIRA = FNE.CLIENTE
                WHERE COALESCE(NEG.VAL_FINANCIADO, 0) > 0
                  AND VP.SITUACAO IN ('7', '9')
                  AND (
                        (VP.TIPO_VENDA IN ('N', 'F') AND VEI.DTA_VENDA BETWEEN :DATA_INICIO AND :DATA_FIM)
                     OR (VP.TIPO_VENDA IN ('D', 'F') AND VEI.DTA_NOTIFICACAO_CREDITO BETWEEN :DATA_INICIO AND :DATA_FIM)
                  )
                  AND VR.TIPO_RETORNO IN ('1')
                  AND (:EMPRESA IS NULL OR INSTR(',' || :EMPRESA || ',', ',' || TO_CHAR(VP.EMPRESA) || ',') > 0)
                  AND (:REVENDA IS NULL OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(VP.REVENDA) || ',') > 0)";

        private const string RetornoFiResumoSql = @"
            SELECT
                COUNT(DISTINCT VP.PROPOSTA) AS CONTRATOS,
                SUM(COALESCE(VR.VAL_RETORNO, 0)) AS RETORNO_TOTAL,
                SUM(COALESCE(NEG.VAL_FINANCIADO, 0)) AS VALOR_FINANCIADO,
                SUM(COALESCE(VEI.VAL_PRESENTE_VENDA, 0)) AS VALOR_VENDA,
                SUM(COALESCE(VR.VAL_COMISSAO, 0)) AS COMISSAO_TOTAL
            " + RetornoFiBaseFromSql;

        private const string RetornoFiFinanceirasSql = @"
            SELECT *
            FROM (
                SELECT
                    COALESCE(FIN.NOME, FNE.NOME, 'Sem financeira') AS NOME,
                    COUNT(DISTINCT VP.PROPOSTA) AS QUANTIDADE,
                    SUM(COALESCE(VR.VAL_RETORNO, 0)) AS RETORNO,
                    SUM(COALESCE(NEG.VAL_FINANCIADO, 0)) AS VALOR_FINANCIADO,
                    SUM(COALESCE(VR.VAL_COMISSAO, 0)) AS COMISSAO
                " + RetornoFiBaseFromSql + @"
                GROUP BY COALESCE(FIN.NOME, FNE.NOME, 'Sem financeira')
                ORDER BY RETORNO DESC
            )
            WHERE ROWNUM <= 10";

        private const string RetornoFiVendedoresSql = @"
            SELECT *
            FROM (
                SELECT
                    COALESCE(VEN.NOME, 'Sem vendedor') AS NOME,
                    COUNT(DISTINCT VP.PROPOSTA) AS QUANTIDADE,
                    SUM(COALESCE(VR.VAL_RETORNO, 0)) AS RETORNO,
                    SUM(COALESCE(NEG.VAL_FINANCIADO, 0)) AS VALOR_FINANCIADO,
                    SUM(COALESCE(VR.VAL_COMISSAO, 0)) AS COMISSAO
                " + RetornoFiBaseFromSql + @"
                GROUP BY COALESCE(VEN.NOME, 'Sem vendedor')
                ORDER BY RETORNO DESC
            )
            WHERE ROWNUM <= 10";

        private const string RetornoFiParcelasSql = @"
            SELECT
                TO_CHAR(COALESCE(NEG.QTD_PARCELAS, 0)) || ' parcelas' AS NOME,
                COUNT(DISTINCT VP.PROPOSTA) AS QUANTIDADE,
                SUM(COALESCE(VR.VAL_RETORNO, 0)) AS RETORNO,
                SUM(COALESCE(NEG.VAL_FINANCIADO, 0)) AS VALOR_FINANCIADO,
                SUM(COALESCE(VR.VAL_COMISSAO, 0)) AS COMISSAO
            " + RetornoFiBaseFromSql + @"
            GROUP BY COALESCE(NEG.QTD_PARCELAS, 0)
            ORDER BY COALESCE(NEG.QTD_PARCELAS, 0)";

        public VeiculosBiService(IConfiguration configuration, AppDbContext context)
        {
            _context = context;
            _connectionString = GetOracleConnectionString(configuration);
        }

        public async Task<List<VeiculoAcessorioRankingDto>> LoadAcessoriosAsync(string role, VeiculosBiFilterDto filter)
        {
            await EnsureCanAccessAsync(role);
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string Oracle nao configurada para o B.I de veiculos.");

            var dataInicio = (filter.DataInicio ?? DateTime.Today.AddMonths(-1)).Date;
            var dataFim = (filter.DataFim ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            if (dataInicio > dataFim)
                throw new InvalidOperationException("Data inicial nao pode ser maior que a data final.");

            var empresa = await ApplyEmpresaScopeAsync(role, NormalizeFilter(filter.Empresa));
            var revenda = NormalizeFilter(filter.Revenda);

            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandText = AcessoriosSql;
            command.CommandType = CommandType.Text;
            command.Parameters.Add("DATA_INICIO", OracleDbType.Date, dataInicio, ParameterDirection.Input);
            command.Parameters.Add("DATA_FIM", OracleDbType.Date, dataFim, ParameterDirection.Input);
            command.Parameters.Add("EMPRESA", OracleDbType.Varchar2, empresa, ParameterDirection.Input);
            command.Parameters.Add("REVENDA", OracleDbType.Varchar2, revenda, ParameterDirection.Input);

            var items = new List<VeiculoAcessorioRankingDto>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var faturamento = GetDecimal(reader, "FATURAMENTO");
                var rentabilidade = GetDecimal(reader, "RENTABILIDADE");
                items.Add(new VeiculoAcessorioRankingDto
                {
                    Codigo = GetString(reader, "CODIGO"),
                    Nome = GetString(reader, "NOME"),
                    Categoria = GetString(reader, "CATEGORIA"),
                    Quantidade = GetInt(reader, "QUANTIDADE"),
                    Faturamento = faturamento,
                    Rentabilidade = rentabilidade,
                    MargemPercentual = faturamento == 0 ? 0 : rentabilidade / faturamento * 100
                });
            }

            return items;
        }

        public async Task<VeiculosBiDashboardDto> LoadDashboardAsync(string role, VeiculosBiFilterDto filter)
        {
            await EnsureCanAccessAsync(role);
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string Oracle nao configurada para o B.I de veiculos.");

            var dataInicio = (filter.DataInicio ?? DateTime.Today.AddDays(-30)).Date;
            var dataFim = (filter.DataFim ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            if (dataInicio > dataFim)
                throw new InvalidOperationException("Data inicial nao pode ser maior que a data final.");

            var empresa = await ApplyEmpresaScopeAsync(role, NormalizeFilter(filter.Empresa));
            var revenda = NormalizeFilter(filter.Revenda);

            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            return new VeiculosBiDashboardDto
            {
                Filiais = await LoadFiliaisAsync(connection, dataInicio, dataFim, empresa, revenda),
                VendasDiarias = await LoadVendasDiariasAsync(connection, dataInicio, dataFim, empresa, revenda),
                Modelos = await LoadModelosAsync(connection, dataInicio, dataFim, empresa, revenda),
                Vendedores = await LoadVendedoresAsync(connection, dataInicio, dataFim, empresa, revenda),
                AtualizadoEm = DateTime.Now
            };
        }

        public async Task<VeiculosBiRetornoFiDashboardDto> LoadRetornoFiAsync(string role, VeiculosBiFilterDto filter)
        {
            await EnsureCanAccessAsync(role);
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string Oracle nao configurada para o B.I de veiculos.");

            var dataInicio = (filter.DataInicio ?? DateTime.Today.AddDays(-30)).Date;
            var dataFim = (filter.DataFim ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            if (dataInicio > dataFim)
                throw new InvalidOperationException("Data inicial nao pode ser maior que a data final.");

            var empresa = await ApplyEmpresaScopeAsync(role, NormalizeFilter(filter.Empresa));
            var revenda = NormalizeFilter(filter.Revenda);

            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var dashboard = await LoadRetornoFiResumoAsync(connection, dataInicio, dataFim, empresa, revenda);
            dashboard.Financeiras = await LoadRetornoFiGruposAsync(connection, RetornoFiFinanceirasSql, dataInicio, dataFim, empresa, revenda);
            dashboard.Vendedores = await LoadRetornoFiGruposAsync(connection, RetornoFiVendedoresSql, dataInicio, dataFim, empresa, revenda);
            dashboard.Parcelas = await LoadRetornoFiGruposAsync(connection, RetornoFiParcelasSql, dataInicio, dataFim, empresa, revenda);
            dashboard.AtualizadoEm = DateTime.Now;
            return dashboard;
        }

        private static async Task<List<VeiculoBiFilialVendaDto>> LoadFiliaisAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, object empresa, object revenda)
        {
            await using var command = CreateCommand(connection, VendasFiliaisSql, dataInicio, dataFim, empresa, revenda);
            var items = new List<VeiculoBiFilialVendaDto>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var novos = GetInt(reader, "FATURADOS_NOVOS");
                var direta = GetInt(reader, "FATURADOS_DIRETA");
                var seminovos = GetInt(reader, "SEMINOVOS");
                items.Add(new VeiculoBiFilialVendaDto
                {
                    EmpresaNumero = GetInt(reader, "EMPRESA_NUMERO"),
                    EmpresaNome = GetString(reader, "EMPRESA_NOME"),
                    RevendaNumero = GetInt(reader, "REVENDA_NUMERO"),
                    Filial = GetString(reader, "FILIAL"),
                    FaturadosNovos = novos,
                    FaturadosDireta = direta,
                    Seminovos = seminovos,
                    AnunciadosNovos = novos,
                    AnunciadosDireta = direta,
                    Propostas = novos + direta + seminovos,
                    Baixados = novos + direta + seminovos,
                    Faturamento = GetDecimal(reader, "FATURAMENTO"),
                    Margem = GetDecimal(reader, "MARGEM")
                });
            }

            return items;
        }

        private static async Task<List<VeiculoBiVendaDiariaDto>> LoadVendasDiariasAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, object empresa, object revenda)
        {
            await using var command = CreateCommand(connection, VendasDiariasSql, dataInicio, dataFim, empresa, revenda);
            var items = new List<VeiculoBiVendaDiariaDto>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var date = GetDateTime(reader, "DATA_VENDA");
                items.Add(new VeiculoBiVendaDiariaDto
                {
                    Data = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Novos = GetInt(reader, "NOVOS"),
                    VendaDireta = GetInt(reader, "VENDA_DIRETA"),
                    Seminovos = GetInt(reader, "SEMINOVOS")
                });
            }

            return items;
        }

        private static async Task<List<VeiculoBiModeloRankingDto>> LoadModelosAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, object empresa, object revenda)
        {
            await using var command = CreateCommand(connection, ModelosSql, dataInicio, dataFim, empresa, revenda);
            var items = new List<VeiculoBiModeloRankingDto>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var faturamento = GetDecimal(reader, "FATURAMENTO");
                var margem = GetDecimal(reader, "MARGEM");
                items.Add(new VeiculoBiModeloRankingDto
                {
                    Modelo = GetString(reader, "MODELO"),
                    Familia = GetString(reader, "FAMILIA"),
                    Unidades = GetInt(reader, "UNIDADES"),
                    Faturamento = faturamento,
                    MargemPercentual = faturamento == 0 ? 0 : margem / faturamento * 100
                });
            }

            return items;
        }

        private static async Task<List<VeiculoBiVendedorMetaDto>> LoadVendedoresAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, object empresa, object revenda)
        {
            await using var command = CreateCommand(connection, VendedoresSql, dataInicio, dataFim, empresa, revenda);
            var items = new List<VeiculoBiVendedorMetaDto>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var realizado = GetInt(reader, "REALIZADO");
                items.Add(new VeiculoBiVendedorMetaDto
                {
                    Vendedor = GetString(reader, "VENDEDOR"),
                    Filial = GetString(reader, "FILIAL"),
                    Meta = 0,
                    Realizado = realizado,
                    Faturamento = GetDecimal(reader, "FATURAMENTO")
                });
            }

            return items;
        }

        private static async Task<VeiculosBiRetornoFiDashboardDto> LoadRetornoFiResumoAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, object empresa, object revenda)
        {
            await using var command = CreateCommand(connection, RetornoFiResumoSql, dataInicio, dataFim, empresa, revenda);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return new VeiculosBiRetornoFiDashboardDto();

            return new VeiculosBiRetornoFiDashboardDto
            {
                Contratos = GetInt(reader, "CONTRATOS"),
                RetornoTotal = GetDecimal(reader, "RETORNO_TOTAL"),
                ValorFinanciado = GetDecimal(reader, "VALOR_FINANCIADO"),
                ValorVenda = GetDecimal(reader, "VALOR_VENDA"),
                ComissaoTotal = GetDecimal(reader, "COMISSAO_TOTAL")
            };
        }

        private static async Task<List<VeiculosBiRetornoFiGrupoDto>> LoadRetornoFiGruposAsync(OracleConnection connection, string sql, DateTime dataInicio, DateTime dataFim, object empresa, object revenda)
        {
            await using var command = CreateCommand(connection, sql, dataInicio, dataFim, empresa, revenda);
            var items = new List<VeiculosBiRetornoFiGrupoDto>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new VeiculosBiRetornoFiGrupoDto
                {
                    Nome = GetString(reader, "NOME"),
                    Quantidade = GetInt(reader, "QUANTIDADE"),
                    Retorno = GetDecimal(reader, "RETORNO"),
                    ValorFinanciado = GetDecimal(reader, "VALOR_FINANCIADO"),
                    Comissao = GetDecimal(reader, "COMISSAO")
                });
            }

            return items;
        }

        private async Task EnsureCanAccessAsync(string role)
        {
            if (RoleScope.IsAdmin(role) || RoleScope.IsTI(role))
                return;

            var perfil = PerfisService.NormalizePerfilName(role);
            var hasAccess = await _context.PerfilSistema
                .Where(p => p.Nome == perfil)
                .SelectMany(p => p.Acessos.Select(a => a.Chave))
                .AnyAsync(acesso => acesso == "veiculos-bi");
            if (!hasAccess)
                throw new UnauthorizedAccessException("Perfil sem acesso ao B.I de veiculos.");
        }

        private async Task<object> ApplyEmpresaScopeAsync(string role, object requestedEmpresa)
        {
            if (RoleScope.IsQualidadeNissan(role))
                return "2";

            var perfil = PerfisService.NormalizePerfilName(role);
            var empresasPermitidas = await _context.PerfilSistema
                .Where(p => p.Nome == perfil)
                .SelectMany(p => p.Empresas.Select(e => e.EmpresaNumero))
                .Distinct()
                .ToListAsync();
            if (empresasPermitidas.Count == 0)
                return requestedEmpresa;

            if (requestedEmpresa == DBNull.Value)
                return string.Join(",", empresasPermitidas);

            var requested = requestedEmpresa.ToString()?.Trim() ?? string.Empty;
            var requestedNumbers = requested
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var numero) ? numero : 0)
                .Where(numero => numero > 0)
                .Distinct()
                .ToList();

            if (requestedNumbers.Any(numero => !empresasPermitidas.Contains(numero)))
                throw new UnauthorizedAccessException("Perfil sem acesso a empresa selecionada no B.I de veiculos.");

            return string.Join(",", requestedNumbers);
        }

        private static object NormalizeFilter(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) || normalized.Equals("Todos", StringComparison.OrdinalIgnoreCase)
                ? DBNull.Value
                : normalized;
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
            if (reader.IsDBNull(ordinal))
                return 0;

            if (reader is OracleDataReader oracleReader)
            {
                var value = oracleReader.GetOracleDecimal(ordinal);
                if (value.IsNull)
                    return 0;

                return decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
            }

            return Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static DateTime GetDateTime(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? DateTime.MinValue : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static int GetInt(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static OracleCommand CreateCommand(OracleConnection connection, string sql, DateTime dataInicio, DateTime dataFim, object empresa, object revenda)
        {
            var command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.Parameters.Add("DATA_INICIO", OracleDbType.Date, dataInicio, ParameterDirection.Input);
            command.Parameters.Add("DATA_FIM", OracleDbType.Date, dataFim, ParameterDirection.Input);
            command.Parameters.Add("EMPRESA", OracleDbType.Varchar2, empresa, ParameterDirection.Input);
            command.Parameters.Add("REVENDA", OracleDbType.Varchar2, revenda, ParameterDirection.Input);
            return command;
        }
    }
}
