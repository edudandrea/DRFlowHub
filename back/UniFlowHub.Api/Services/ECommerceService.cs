using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using System.Data.Common;
using UniFlowHub.Api.Data;
using UniFlowHub.Api.Dtos.ECommerce;

namespace UniFlowHub.Api.Services
{
    public class ECommerceService
    {
        private readonly AppDbContext _context;
        private readonly string _connectionString;
        private sealed record RegisteredUnit(int EmpresaNumero, int NumeroRevenda, string Nome, string Empresa);

        private const string DashboardSql = @"
            WITH VENDAS AS (
                SELECT
                    FMC.EMPRESA,
                    FMC.REVENDA,
                    FMC.NUMERO_NOTA_FISCAL,
                    FMC.SERIE_NOTA_FISCAL,
                    FMC.TIPO_TRANSACAO,
                    FMC.CONTADOR,
                    VEN.VENDEDOR,
                    VEN.NOME AS NOME_VENDEDOR,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -1 ELSE 1 END AS NOTA_PESO,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -ABS(SUM(
                        COALESCE(FMI.VAL_TOTAL_REAL_ITEM, 0)
                        - (COALESCE(FMI.VAL_DESCONTO, 0) - COALESCE(FMI.VAL_DESCONTO_FRANQUIA, 0))
                        + COALESCE(FMI.VAL_FRETE, 0)
                    )) ELSE SUM(
                        COALESCE(FMI.VAL_TOTAL_REAL_ITEM, 0)
                        - (COALESCE(FMI.VAL_DESCONTO, 0) - COALESCE(FMI.VAL_DESCONTO_FRANQUIA, 0))
                        + COALESCE(FMI.VAL_FRETE, 0)
                    ) END AS VALOR_VENDA,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -ABS(SUM(COALESCE(FMI.VAL_CUSTO_MEDIO, 0))) ELSE SUM(COALESCE(FMI.VAL_CUSTO_MEDIO, 0)) END AS CUSTO,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -ABS(SUM(
                        ((COALESCE(FMI.BASE_ICMS, 0) * COALESCE(FMI.ALIQUOTA_ICMS, 0) / 100) - COALESCE(FMI.VAL_ICMS_DIFERIDO, 0))
                        + COALESCE(FMI.VAL_PIS, 0)
                        + COALESCE(FMI.VAL_COFINS, 0)
                        + COALESCE(FMI.VAL_IPI, 0)
                        + COALESCE(FMI.VAL_ICMS_RETIDO, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_DEST, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_REM, 0)
                        + COALESCE(FMI.DIFERENCA_ICMS_REDUZIDO, 0)
                    )) ELSE SUM(
                        ((COALESCE(FMI.BASE_ICMS, 0) * COALESCE(FMI.ALIQUOTA_ICMS, 0) / 100) - COALESCE(FMI.VAL_ICMS_DIFERIDO, 0))
                        + COALESCE(FMI.VAL_PIS, 0)
                        + COALESCE(FMI.VAL_COFINS, 0)
                        + COALESCE(FMI.VAL_IPI, 0)
                        + COALESCE(FMI.VAL_ICMS_RETIDO, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_DEST, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_REM, 0)
                        + COALESCE(FMI.DIFERENCA_ICMS_REDUZIDO, 0)
                    ) END AS IMPOSTOS,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -ABS(SUM(COALESCE(FMI.VAL_DESPESA_RENTABILIDADE, 0))) ELSE SUM(COALESCE(FMI.VAL_DESPESA_RENTABILIDADE, 0)) END AS DESPESAS
                FROM FAT_MOVIMENTO_ITEM FMI
                INNER JOIN PEC_ITEM_ESTOQUE PIE
                   ON PIE.EMPRESA = FMI.EMPRESA
                  AND PIE.ITEM_ESTOQUE = FMI.ITEM_ESTOQUE
                INNER JOIN PEC_ITEM_REVENDA PIR
                   ON PIR.EMPRESA = FMI.EMPRESA
                  AND PIR.REVENDA = FMI.REVENDA
                  AND PIR.ITEM_ESTOQUE = FMI.ITEM_ESTOQUE
                INNER JOIN FAT_MOVIMENTO_CAPA FMC
                   ON FMC.EMPRESA = FMI.EMPRESA
                  AND FMC.REVENDA = FMI.REVENDA
                  AND FMC.NUMERO_NOTA_FISCAL = FMI.NUMERO_NOTA_FISCAL
                  AND FMC.SERIE_NOTA_FISCAL = FMI.SERIE_NOTA_FISCAL
                  AND FMC.TIPO_TRANSACAO = FMI.TIPO_TRANSACAO
                  AND FMC.CONTADOR = FMI.CONTADOR
                INNER JOIN FAT_TIPO_TRANSACAO TT
                   ON TT.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                INNER JOIN FAT_NOTAS_VENDEDOR FNV
                   ON FNV.EMPRESA = FMC.EMPRESA
                  AND FNV.REVENDA = FMC.REVENDA
                  AND FNV.NUMERO_NOTA_FISCAL = FMC.NUMERO_NOTA_FISCAL
                  AND FNV.SERIE_NOTA_FISCAL = FMC.SERIE_NOTA_FISCAL
                  AND FNV.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                  AND FNV.CONTADOR = FMC.CONTADOR
                  AND (FNV.TIPO_VENDEDOR = 'N' OR FNV.TIPO_VENDEDOR IS NULL)
                INNER JOIN FAT_VENDEDOR VEN
                   ON VEN.EMPRESA = FNV.EMPRESA
                  AND VEN.REVENDA = FNV.REVENDA
                  AND VEN.VENDEDOR = FNV.VENDEDOR
                INNER JOIN FAT_CLIENTE CLI
                   ON CLI.CLIENTE = FMC.CLIENTE
                WHERE FMC.STATUS = 'F'
                  AND TT.TIPO = 'S'
                  AND TT.TIPO_TRANSACAO IN ('F21', 'P41')
                  AND TT.SUBTIPO_TRANSACAO = 'N'
                  AND FMC.DEPARTAMENTO IN (3)
                  AND PIE.TIPO_INDUSTRIALIZACAO IS NULL
                  AND FMC.DTA_ENTRADA_SAIDA BETWEEN :DATA_INICIO AND :DATA_FIM
                  AND (:EMPRESA IS NULL OR INSTR(',' || :EMPRESA || ',', ',' || TO_CHAR(FMC.EMPRESA) || ',') > 0)
                  AND (
                    :REVENDA IS NULL
                    OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(FMC.EMPRESA) || ':' || TO_CHAR(FMC.REVENDA) || ',') > 0
                    OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(FMC.REVENDA) || ',') > 0
                  )
                GROUP BY
                    FMC.EMPRESA,
                    FMC.REVENDA,
                    FMC.NUMERO_NOTA_FISCAL,
                    FMC.SERIE_NOTA_FISCAL,
                    FMC.TIPO_TRANSACAO,
                    FMC.CONTADOR,
                    VEN.VENDEDOR,
                    VEN.NOME

                UNION ALL

                SELECT
                    FMC.EMPRESA,
                    FMC.REVENDA,
                    FMC.NUMERO_NOTA_FISCAL,
                    FMC.SERIE_NOTA_FISCAL,
                    FMC.TIPO_TRANSACAO,
                    FMC.CONTADOR,
                    VEN.VENDEDOR,
                    VEN.NOME AS NOME_VENDEDOR,
                    0 AS NOTA_PESO,
                    -ABS(SUM(
                        COALESCE(FMI.VAL_TOTAL_REAL_ITEM, 0)
                        - (COALESCE(FMI.VAL_DESCONTO, 0) - COALESCE(FMI.VAL_DESCONTO_FRANQUIA, 0))
                        + COALESCE(FMI.VAL_FRETE, 0)
                    )) AS VALOR_VENDA,
                    -ABS(SUM(COALESCE(FMI.VAL_CUSTO_MEDIO, 0))) AS CUSTO,
                    -ABS(SUM(
                        ((COALESCE(FMI.BASE_ICMS, 0) * COALESCE(FMI.ALIQUOTA_ICMS, 0) / 100) - COALESCE(FMI.VAL_ICMS_DIFERIDO, 0))
                        + COALESCE(FMI.VAL_PIS, 0)
                        + COALESCE(FMI.VAL_COFINS, 0)
                        + COALESCE(FMI.VAL_IPI, 0)
                        + COALESCE(FMI.VAL_ICMS_RETIDO, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_DEST, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_REM, 0)
                        + COALESCE(FMI.DIFERENCA_ICMS_REDUZIDO, 0)
                    )) AS IMPOSTOS,
                    -ABS(SUM(COALESCE(FMI.VAL_DESPESA_RENTABILIDADE, 0))) AS DESPESAS
                FROM FAT_MOVIMENTO_ITEM FMI
                INNER JOIN PEC_ITEM_ESTOQUE PIE
                   ON PIE.EMPRESA = FMI.EMPRESA
                  AND PIE.ITEM_ESTOQUE = FMI.ITEM_ESTOQUE
                INNER JOIN FAT_MOVIMENTO_CAPA FMC
                   ON FMC.EMPRESA = FMI.EMPRESA
                  AND FMC.REVENDA = FMI.REVENDA
                  AND FMC.NUMERO_NOTA_FISCAL = FMI.NUMERO_NOTA_FISCAL
                  AND FMC.SERIE_NOTA_FISCAL = FMI.SERIE_NOTA_FISCAL
                  AND FMC.TIPO_TRANSACAO = FMI.TIPO_TRANSACAO
                  AND FMC.CONTADOR = FMI.CONTADOR
                INNER JOIN FAT_MOVIMENTO_CAPA FMCORI
                   ON FMCORI.EMPRESA = FMC.EMPRESA
                  AND FMCORI.REVENDA = FMC.REVENDA
                  AND FMCORI.FATOPERACAO = FMC.FATOPERACAO_ORIGINAL
                INNER JOIN FAT_TIPO_TRANSACAO TT
                   ON TT.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                INNER JOIN FAT_NOTAS_VENDEDOR FNV
                   ON FNV.EMPRESA = FMC.EMPRESA
                  AND FNV.REVENDA = FMC.REVENDA
                  AND FNV.NUMERO_NOTA_FISCAL = FMC.NUMERO_NOTA_FISCAL
                  AND FNV.SERIE_NOTA_FISCAL = FMC.SERIE_NOTA_FISCAL
                  AND FNV.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                  AND FNV.CONTADOR = FMC.CONTADOR
                  AND (FNV.TIPO_VENDEDOR = 'N' OR FNV.TIPO_VENDEDOR IS NULL)
                INNER JOIN FAT_VENDEDOR VEN
                   ON VEN.EMPRESA = FNV.EMPRESA
                  AND VEN.REVENDA = FNV.REVENDA
                  AND VEN.VENDEDOR = FNV.VENDEDOR
                WHERE EXISTS (
                    SELECT FMIORI.EMPRESA
                    FROM FAT_MOVIMENTO_ITEM FMIORI
                    WHERE FMCORI.EMPRESA = FMIORI.EMPRESA
                      AND FMCORI.REVENDA = FMIORI.REVENDA
                      AND FMCORI.NUMERO_NOTA_FISCAL = FMIORI.NUMERO_NOTA_FISCAL
                      AND FMCORI.SERIE_NOTA_FISCAL = FMIORI.SERIE_NOTA_FISCAL
                      AND FMCORI.TIPO_TRANSACAO = FMIORI.TIPO_TRANSACAO
                      AND FMCORI.CONTADOR = FMIORI.CONTADOR
                      AND FMI.ITEM_ESTOQUE = FMIORI.ITEM_ESTOQUE
                )
                  AND FMC.STATUS = 'F'
                  AND TT.TIPO_TRANSACAO IN ('P77')
                  AND FMC.FATOPERACAO_ORIGINAL IS NOT NULL
                  AND FMC.DEPARTAMENTO IN (3)
                  AND PIE.TIPO_INDUSTRIALIZACAO IS NULL
                  AND FMC.DTA_ENTRADA_SAIDA BETWEEN :DATA_INICIO AND :DATA_FIM
                  AND (:EMPRESA IS NULL OR INSTR(',' || :EMPRESA || ',', ',' || TO_CHAR(FMC.EMPRESA) || ',') > 0)
                  AND (
                    :REVENDA IS NULL
                    OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(FMC.EMPRESA) || ':' || TO_CHAR(FMC.REVENDA) || ',') > 0
                    OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(FMC.REVENDA) || ',') > 0
                  )
                GROUP BY
                    FMC.EMPRESA,
                    FMC.REVENDA,
                    FMC.NUMERO_NOTA_FISCAL,
                    FMC.SERIE_NOTA_FISCAL,
                    FMC.TIPO_TRANSACAO,
                    FMC.CONTADOR,
                    VEN.VENDEDOR,
                    VEN.NOME
            )
            SELECT
                EMPRESA,
                REVENDA,
                VENDEDOR,
                NOME_VENDEDOR,
                SUM(NOTA_PESO) AS NOTAS_EMITIDAS,
                SUM(VALOR_VENDA) AS REALIZADO,
                SUM(CUSTO) AS CUSTO,
                SUM(IMPOSTOS) AS IMPOSTOS,
                SUM(DESPESAS) AS DESPESAS,
                SUM(VALOR_VENDA - CUSTO - IMPOSTOS - DESPESAS) AS MARGEM_CONTRIBUICAO
            FROM VENDAS
            GROUP BY EMPRESA, REVENDA, VENDEDOR, NOME_VENDEDOR
            ORDER BY REALIZADO DESC";

        private const string AnnualSql = @"
            WITH VENDAS AS (
                SELECT
                    EXTRACT(YEAR FROM FMC.DTA_ENTRADA_SAIDA) AS ANO,
                    FMC.EMPRESA,
                    FMC.REVENDA,
                    FMC.NUMERO_NOTA_FISCAL,
                    FMC.SERIE_NOTA_FISCAL,
                    FMC.TIPO_TRANSACAO,
                    FMC.CONTADOR,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -1 ELSE 1 END AS NOTA_PESO,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -ABS(SUM(
                        COALESCE(FMI.VAL_TOTAL_REAL_ITEM, 0)
                        - (COALESCE(FMI.VAL_DESCONTO, 0) - COALESCE(FMI.VAL_DESCONTO_FRANQUIA, 0))
                        + COALESCE(FMI.VAL_FRETE, 0)
                    )) ELSE SUM(
                        COALESCE(FMI.VAL_TOTAL_REAL_ITEM, 0)
                        - (COALESCE(FMI.VAL_DESCONTO, 0) - COALESCE(FMI.VAL_DESCONTO_FRANQUIA, 0))
                        + COALESCE(FMI.VAL_FRETE, 0)
                    ) END AS VALOR_VENDA,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -ABS(SUM(COALESCE(FMI.VAL_CUSTO_MEDIO, 0))) ELSE SUM(COALESCE(FMI.VAL_CUSTO_MEDIO, 0)) END AS CUSTO,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -ABS(SUM(
                        ((COALESCE(FMI.BASE_ICMS, 0) * COALESCE(FMI.ALIQUOTA_ICMS, 0) / 100) - COALESCE(FMI.VAL_ICMS_DIFERIDO, 0))
                        + COALESCE(FMI.VAL_PIS, 0)
                        + COALESCE(FMI.VAL_COFINS, 0)
                        + COALESCE(FMI.VAL_IPI, 0)
                        + COALESCE(FMI.VAL_ICMS_RETIDO, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_DEST, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_REM, 0)
                        + COALESCE(FMI.DIFERENCA_ICMS_REDUZIDO, 0)
                    )) ELSE SUM(
                        ((COALESCE(FMI.BASE_ICMS, 0) * COALESCE(FMI.ALIQUOTA_ICMS, 0) / 100) - COALESCE(FMI.VAL_ICMS_DIFERIDO, 0))
                        + COALESCE(FMI.VAL_PIS, 0)
                        + COALESCE(FMI.VAL_COFINS, 0)
                        + COALESCE(FMI.VAL_IPI, 0)
                        + COALESCE(FMI.VAL_ICMS_RETIDO, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_DEST, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_REM, 0)
                        + COALESCE(FMI.DIFERENCA_ICMS_REDUZIDO, 0)
                    ) END AS IMPOSTOS,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -ABS(SUM(COALESCE(FMI.VAL_DESPESA_RENTABILIDADE, 0))) ELSE SUM(COALESCE(FMI.VAL_DESPESA_RENTABILIDADE, 0)) END AS DESPESAS
                FROM FAT_MOVIMENTO_ITEM FMI
                INNER JOIN PEC_ITEM_ESTOQUE PIE
                   ON PIE.EMPRESA = FMI.EMPRESA
                  AND PIE.ITEM_ESTOQUE = FMI.ITEM_ESTOQUE
                INNER JOIN PEC_ITEM_REVENDA PIR
                   ON PIR.EMPRESA = FMI.EMPRESA
                  AND PIR.REVENDA = FMI.REVENDA
                  AND PIR.ITEM_ESTOQUE = FMI.ITEM_ESTOQUE
                INNER JOIN FAT_MOVIMENTO_CAPA FMC
                   ON FMC.EMPRESA = FMI.EMPRESA
                  AND FMC.REVENDA = FMI.REVENDA
                  AND FMC.NUMERO_NOTA_FISCAL = FMI.NUMERO_NOTA_FISCAL
                  AND FMC.SERIE_NOTA_FISCAL = FMI.SERIE_NOTA_FISCAL
                  AND FMC.TIPO_TRANSACAO = FMI.TIPO_TRANSACAO
                  AND FMC.CONTADOR = FMI.CONTADOR
                INNER JOIN FAT_TIPO_TRANSACAO TT
                   ON TT.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                INNER JOIN FAT_NOTAS_VENDEDOR FNV
                   ON FNV.EMPRESA = FMC.EMPRESA
                  AND FNV.REVENDA = FMC.REVENDA
                  AND FNV.NUMERO_NOTA_FISCAL = FMC.NUMERO_NOTA_FISCAL
                  AND FNV.SERIE_NOTA_FISCAL = FMC.SERIE_NOTA_FISCAL
                  AND FNV.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                  AND FNV.CONTADOR = FMC.CONTADOR
                  AND (FNV.TIPO_VENDEDOR = 'N' OR FNV.TIPO_VENDEDOR IS NULL)
                INNER JOIN FAT_VENDEDOR VEN
                   ON VEN.EMPRESA = FNV.EMPRESA
                  AND VEN.REVENDA = FNV.REVENDA
                  AND VEN.VENDEDOR = FNV.VENDEDOR
                INNER JOIN FAT_CLIENTE CLI
                   ON CLI.CLIENTE = FMC.CLIENTE
                WHERE FMC.STATUS = 'F'
                  AND TT.TIPO = 'S'
                  AND TT.TIPO_TRANSACAO IN ('F21', 'P41')
                  AND TT.SUBTIPO_TRANSACAO = 'N'
                  AND FMC.DEPARTAMENTO IN (3)
                  AND PIE.TIPO_INDUSTRIALIZACAO IS NULL
                  AND FMC.DTA_ENTRADA_SAIDA BETWEEN :DATA_INICIO AND :DATA_FIM
                  AND (:EMPRESA IS NULL OR INSTR(',' || :EMPRESA || ',', ',' || TO_CHAR(FMC.EMPRESA) || ',') > 0)
                  AND (
                    :REVENDA IS NULL
                    OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(FMC.EMPRESA) || ':' || TO_CHAR(FMC.REVENDA) || ',') > 0
                    OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(FMC.REVENDA) || ',') > 0
                  )
                GROUP BY
                    EXTRACT(YEAR FROM FMC.DTA_ENTRADA_SAIDA),
                    FMC.EMPRESA,
                    FMC.REVENDA,
                    FMC.NUMERO_NOTA_FISCAL,
                    FMC.SERIE_NOTA_FISCAL,
                    FMC.TIPO_TRANSACAO,
                    FMC.CONTADOR

                UNION ALL

                SELECT
                    EXTRACT(YEAR FROM FMC.DTA_ENTRADA_SAIDA) AS ANO,
                    FMC.EMPRESA,
                    FMC.REVENDA,
                    FMC.NUMERO_NOTA_FISCAL,
                    FMC.SERIE_NOTA_FISCAL,
                    FMC.TIPO_TRANSACAO,
                    FMC.CONTADOR,
                    0 AS NOTA_PESO,
                    -ABS(SUM(
                        COALESCE(FMI.VAL_TOTAL_REAL_ITEM, 0)
                        - (COALESCE(FMI.VAL_DESCONTO, 0) - COALESCE(FMI.VAL_DESCONTO_FRANQUIA, 0))
                        + COALESCE(FMI.VAL_FRETE, 0)
                    )) AS VALOR_VENDA,
                    -ABS(SUM(COALESCE(FMI.VAL_CUSTO_MEDIO, 0))) AS CUSTO,
                    -ABS(SUM(
                        ((COALESCE(FMI.BASE_ICMS, 0) * COALESCE(FMI.ALIQUOTA_ICMS, 0) / 100) - COALESCE(FMI.VAL_ICMS_DIFERIDO, 0))
                        + COALESCE(FMI.VAL_PIS, 0)
                        + COALESCE(FMI.VAL_COFINS, 0)
                        + COALESCE(FMI.VAL_IPI, 0)
                        + COALESCE(FMI.VAL_ICMS_RETIDO, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_DEST, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_REM, 0)
                        + COALESCE(FMI.DIFERENCA_ICMS_REDUZIDO, 0)
                    )) AS IMPOSTOS,
                    -ABS(SUM(COALESCE(FMI.VAL_DESPESA_RENTABILIDADE, 0))) AS DESPESAS
                FROM FAT_MOVIMENTO_ITEM FMI
                INNER JOIN PEC_ITEM_ESTOQUE PIE
                   ON PIE.EMPRESA = FMI.EMPRESA
                  AND PIE.ITEM_ESTOQUE = FMI.ITEM_ESTOQUE
                INNER JOIN FAT_MOVIMENTO_CAPA FMC
                   ON FMC.EMPRESA = FMI.EMPRESA
                  AND FMC.REVENDA = FMI.REVENDA
                  AND FMC.NUMERO_NOTA_FISCAL = FMI.NUMERO_NOTA_FISCAL
                  AND FMC.SERIE_NOTA_FISCAL = FMI.SERIE_NOTA_FISCAL
                  AND FMC.TIPO_TRANSACAO = FMI.TIPO_TRANSACAO
                  AND FMC.CONTADOR = FMI.CONTADOR
                INNER JOIN FAT_MOVIMENTO_CAPA FMCORI
                   ON FMCORI.EMPRESA = FMC.EMPRESA
                  AND FMCORI.REVENDA = FMC.REVENDA
                  AND FMCORI.FATOPERACAO = FMC.FATOPERACAO_ORIGINAL
                INNER JOIN FAT_TIPO_TRANSACAO TT
                   ON TT.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                WHERE EXISTS (
                    SELECT FMIORI.EMPRESA
                    FROM FAT_MOVIMENTO_ITEM FMIORI
                    WHERE FMCORI.EMPRESA = FMIORI.EMPRESA
                      AND FMCORI.REVENDA = FMIORI.REVENDA
                      AND FMCORI.NUMERO_NOTA_FISCAL = FMIORI.NUMERO_NOTA_FISCAL
                      AND FMCORI.SERIE_NOTA_FISCAL = FMIORI.SERIE_NOTA_FISCAL
                      AND FMCORI.TIPO_TRANSACAO = FMIORI.TIPO_TRANSACAO
                      AND FMCORI.CONTADOR = FMIORI.CONTADOR
                      AND FMI.ITEM_ESTOQUE = FMIORI.ITEM_ESTOQUE
                )
                  AND FMC.STATUS = 'F'
                  AND TT.TIPO_TRANSACAO IN ('P77')
                  AND FMC.FATOPERACAO_ORIGINAL IS NOT NULL
                  AND FMC.DEPARTAMENTO IN (3)
                  AND PIE.TIPO_INDUSTRIALIZACAO IS NULL
                  AND FMC.DTA_ENTRADA_SAIDA BETWEEN :DATA_INICIO AND :DATA_FIM
                  AND (:EMPRESA IS NULL OR INSTR(',' || :EMPRESA || ',', ',' || TO_CHAR(FMC.EMPRESA) || ',') > 0)
                  AND (
                    :REVENDA IS NULL
                    OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(FMC.EMPRESA) || ':' || TO_CHAR(FMC.REVENDA) || ',') > 0
                    OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(FMC.REVENDA) || ',') > 0
                  )
                GROUP BY
                    EXTRACT(YEAR FROM FMC.DTA_ENTRADA_SAIDA),
                    FMC.EMPRESA,
                    FMC.REVENDA,
                    FMC.NUMERO_NOTA_FISCAL,
                    FMC.SERIE_NOTA_FISCAL,
                    FMC.TIPO_TRANSACAO,
                    FMC.CONTADOR
            )
            SELECT
                ANO,
                SUM(NOTA_PESO) AS NOTAS_EMITIDAS,
                SUM(VALOR_VENDA) AS REALIZADO,
                SUM(VALOR_VENDA - CUSTO - IMPOSTOS - DESPESAS) AS MARGEM_CONTRIBUICAO
            FROM VENDAS
            GROUP BY ANO
            ORDER BY ANO";

        private const string MonthlySql = @"
            WITH VENDAS AS (
                SELECT
                    EXTRACT(YEAR FROM FMC.DTA_ENTRADA_SAIDA) AS ANO,
                    EXTRACT(MONTH FROM FMC.DTA_ENTRADA_SAIDA) AS MES,
                    FMC.EMPRESA,
                    FMC.REVENDA,
                    FMC.NUMERO_NOTA_FISCAL,
                    FMC.SERIE_NOTA_FISCAL,
                    FMC.TIPO_TRANSACAO,
                    FMC.CONTADOR,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -1 ELSE 1 END AS NOTA_PESO,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -ABS(SUM(
                        COALESCE(FMI.VAL_TOTAL_REAL_ITEM, 0)
                        - (COALESCE(FMI.VAL_DESCONTO, 0) - COALESCE(FMI.VAL_DESCONTO_FRANQUIA, 0))
                        + COALESCE(FMI.VAL_FRETE, 0)
                    )) ELSE SUM(
                        COALESCE(FMI.VAL_TOTAL_REAL_ITEM, 0)
                        - (COALESCE(FMI.VAL_DESCONTO, 0) - COALESCE(FMI.VAL_DESCONTO_FRANQUIA, 0))
                        + COALESCE(FMI.VAL_FRETE, 0)
                    ) END AS VALOR_VENDA,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -ABS(SUM(COALESCE(FMI.VAL_CUSTO_MEDIO, 0))) ELSE SUM(COALESCE(FMI.VAL_CUSTO_MEDIO, 0)) END AS CUSTO,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -ABS(SUM(
                        ((COALESCE(FMI.BASE_ICMS, 0) * COALESCE(FMI.ALIQUOTA_ICMS, 0) / 100) - COALESCE(FMI.VAL_ICMS_DIFERIDO, 0))
                        + COALESCE(FMI.VAL_PIS, 0)
                        + COALESCE(FMI.VAL_COFINS, 0)
                        + COALESCE(FMI.VAL_IPI, 0)
                        + COALESCE(FMI.VAL_ICMS_RETIDO, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_DEST, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_REM, 0)
                        + COALESCE(FMI.DIFERENCA_ICMS_REDUZIDO, 0)
                    )) ELSE SUM(
                        ((COALESCE(FMI.BASE_ICMS, 0) * COALESCE(FMI.ALIQUOTA_ICMS, 0) / 100) - COALESCE(FMI.VAL_ICMS_DIFERIDO, 0))
                        + COALESCE(FMI.VAL_PIS, 0)
                        + COALESCE(FMI.VAL_COFINS, 0)
                        + COALESCE(FMI.VAL_IPI, 0)
                        + COALESCE(FMI.VAL_ICMS_RETIDO, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_DEST, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_REM, 0)
                        + COALESCE(FMI.DIFERENCA_ICMS_REDUZIDO, 0)
                    ) END AS IMPOSTOS,
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -ABS(SUM(COALESCE(FMI.VAL_DESPESA_RENTABILIDADE, 0))) ELSE SUM(COALESCE(FMI.VAL_DESPESA_RENTABILIDADE, 0)) END AS DESPESAS
                FROM FAT_MOVIMENTO_ITEM FMI
                INNER JOIN PEC_ITEM_ESTOQUE PIE
                   ON PIE.EMPRESA = FMI.EMPRESA
                  AND PIE.ITEM_ESTOQUE = FMI.ITEM_ESTOQUE
                INNER JOIN PEC_ITEM_REVENDA PIR
                   ON PIR.EMPRESA = FMI.EMPRESA
                  AND PIR.REVENDA = FMI.REVENDA
                  AND PIR.ITEM_ESTOQUE = FMI.ITEM_ESTOQUE
                INNER JOIN FAT_MOVIMENTO_CAPA FMC
                   ON FMC.EMPRESA = FMI.EMPRESA
                  AND FMC.REVENDA = FMI.REVENDA
                  AND FMC.NUMERO_NOTA_FISCAL = FMI.NUMERO_NOTA_FISCAL
                  AND FMC.SERIE_NOTA_FISCAL = FMI.SERIE_NOTA_FISCAL
                  AND FMC.TIPO_TRANSACAO = FMI.TIPO_TRANSACAO
                  AND FMC.CONTADOR = FMI.CONTADOR
                INNER JOIN FAT_TIPO_TRANSACAO TT
                   ON TT.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                INNER JOIN FAT_NOTAS_VENDEDOR FNV
                   ON FNV.EMPRESA = FMC.EMPRESA
                  AND FNV.REVENDA = FMC.REVENDA
                  AND FNV.NUMERO_NOTA_FISCAL = FMC.NUMERO_NOTA_FISCAL
                  AND FNV.SERIE_NOTA_FISCAL = FMC.SERIE_NOTA_FISCAL
                  AND FNV.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                  AND FNV.CONTADOR = FMC.CONTADOR
                  AND (FNV.TIPO_VENDEDOR = 'N' OR FNV.TIPO_VENDEDOR IS NULL)
                INNER JOIN FAT_VENDEDOR VEN
                   ON VEN.EMPRESA = FNV.EMPRESA
                  AND VEN.REVENDA = FNV.REVENDA
                  AND VEN.VENDEDOR = FNV.VENDEDOR
                INNER JOIN FAT_CLIENTE CLI
                   ON CLI.CLIENTE = FMC.CLIENTE
                WHERE FMC.STATUS = 'F'
                  AND TT.TIPO = 'S'
                  AND TT.TIPO_TRANSACAO IN ('F21', 'P41')
                  AND TT.SUBTIPO_TRANSACAO = 'N'
                  AND FMC.DEPARTAMENTO IN (3)
                  AND PIE.TIPO_INDUSTRIALIZACAO IS NULL
                  AND FMC.DTA_ENTRADA_SAIDA BETWEEN :DATA_INICIO AND :DATA_FIM
                  AND (:EMPRESA IS NULL OR INSTR(',' || :EMPRESA || ',', ',' || TO_CHAR(FMC.EMPRESA) || ',') > 0)
                  AND (
                    :REVENDA IS NULL
                    OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(FMC.EMPRESA) || ':' || TO_CHAR(FMC.REVENDA) || ',') > 0
                    OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(FMC.REVENDA) || ',') > 0
                  )
                GROUP BY
                    EXTRACT(YEAR FROM FMC.DTA_ENTRADA_SAIDA),
                    EXTRACT(MONTH FROM FMC.DTA_ENTRADA_SAIDA),
                    FMC.EMPRESA,
                    FMC.REVENDA,
                    FMC.NUMERO_NOTA_FISCAL,
                    FMC.SERIE_NOTA_FISCAL,
                    FMC.TIPO_TRANSACAO,
                    FMC.CONTADOR

                UNION ALL

                SELECT
                    EXTRACT(YEAR FROM FMC.DTA_ENTRADA_SAIDA) AS ANO,
                    EXTRACT(MONTH FROM FMC.DTA_ENTRADA_SAIDA) AS MES,
                    FMC.EMPRESA,
                    FMC.REVENDA,
                    FMC.NUMERO_NOTA_FISCAL,
                    FMC.SERIE_NOTA_FISCAL,
                    FMC.TIPO_TRANSACAO,
                    FMC.CONTADOR,
                    0 AS NOTA_PESO,
                    -ABS(SUM(
                        COALESCE(FMI.VAL_TOTAL_REAL_ITEM, 0)
                        - (COALESCE(FMI.VAL_DESCONTO, 0) - COALESCE(FMI.VAL_DESCONTO_FRANQUIA, 0))
                        + COALESCE(FMI.VAL_FRETE, 0)
                    )) AS VALOR_VENDA,
                    -ABS(SUM(COALESCE(FMI.VAL_CUSTO_MEDIO, 0))) AS CUSTO,
                    -ABS(SUM(
                        ((COALESCE(FMI.BASE_ICMS, 0) * COALESCE(FMI.ALIQUOTA_ICMS, 0) / 100) - COALESCE(FMI.VAL_ICMS_DIFERIDO, 0))
                        + COALESCE(FMI.VAL_PIS, 0)
                        + COALESCE(FMI.VAL_COFINS, 0)
                        + COALESCE(FMI.VAL_IPI, 0)
                        + COALESCE(FMI.VAL_ICMS_RETIDO, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_DEST, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_REM, 0)
                        + COALESCE(FMI.DIFERENCA_ICMS_REDUZIDO, 0)
                    )) AS IMPOSTOS,
                    -ABS(SUM(COALESCE(FMI.VAL_DESPESA_RENTABILIDADE, 0))) AS DESPESAS
                FROM FAT_MOVIMENTO_ITEM FMI
                INNER JOIN PEC_ITEM_ESTOQUE PIE
                   ON PIE.EMPRESA = FMI.EMPRESA
                  AND PIE.ITEM_ESTOQUE = FMI.ITEM_ESTOQUE
                INNER JOIN FAT_MOVIMENTO_CAPA FMC
                   ON FMC.EMPRESA = FMI.EMPRESA
                  AND FMC.REVENDA = FMI.REVENDA
                  AND FMC.NUMERO_NOTA_FISCAL = FMI.NUMERO_NOTA_FISCAL
                  AND FMC.SERIE_NOTA_FISCAL = FMI.SERIE_NOTA_FISCAL
                  AND FMC.TIPO_TRANSACAO = FMI.TIPO_TRANSACAO
                  AND FMC.CONTADOR = FMI.CONTADOR
                INNER JOIN FAT_MOVIMENTO_CAPA FMCORI
                   ON FMCORI.EMPRESA = FMC.EMPRESA
                  AND FMCORI.REVENDA = FMC.REVENDA
                  AND FMCORI.FATOPERACAO = FMC.FATOPERACAO_ORIGINAL
                INNER JOIN FAT_TIPO_TRANSACAO TT
                   ON TT.TIPO_TRANSACAO = FMC.TIPO_TRANSACAO
                WHERE EXISTS (
                    SELECT FMIORI.EMPRESA
                    FROM FAT_MOVIMENTO_ITEM FMIORI
                    WHERE FMCORI.EMPRESA = FMIORI.EMPRESA
                      AND FMCORI.REVENDA = FMIORI.REVENDA
                      AND FMCORI.NUMERO_NOTA_FISCAL = FMIORI.NUMERO_NOTA_FISCAL
                      AND FMCORI.SERIE_NOTA_FISCAL = FMIORI.SERIE_NOTA_FISCAL
                      AND FMCORI.TIPO_TRANSACAO = FMIORI.TIPO_TRANSACAO
                      AND FMCORI.CONTADOR = FMIORI.CONTADOR
                      AND FMI.ITEM_ESTOQUE = FMIORI.ITEM_ESTOQUE
                )
                  AND FMC.STATUS = 'F'
                  AND TT.TIPO_TRANSACAO IN ('P77')
                  AND FMC.FATOPERACAO_ORIGINAL IS NOT NULL
                  AND FMC.DEPARTAMENTO IN (3)
                  AND PIE.TIPO_INDUSTRIALIZACAO IS NULL
                  AND FMC.DTA_ENTRADA_SAIDA BETWEEN :DATA_INICIO AND :DATA_FIM
                  AND (:EMPRESA IS NULL OR INSTR(',' || :EMPRESA || ',', ',' || TO_CHAR(FMC.EMPRESA) || ',') > 0)
                  AND (
                    :REVENDA IS NULL
                    OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(FMC.EMPRESA) || ':' || TO_CHAR(FMC.REVENDA) || ',') > 0
                    OR INSTR(',' || :REVENDA || ',', ',' || TO_CHAR(FMC.REVENDA) || ',') > 0
                  )
                GROUP BY
                    EXTRACT(YEAR FROM FMC.DTA_ENTRADA_SAIDA),
                    EXTRACT(MONTH FROM FMC.DTA_ENTRADA_SAIDA),
                    FMC.EMPRESA,
                    FMC.REVENDA,
                    FMC.NUMERO_NOTA_FISCAL,
                    FMC.SERIE_NOTA_FISCAL,
                    FMC.TIPO_TRANSACAO,
                    FMC.CONTADOR
            )
            SELECT
                ANO,
                MES,
                SUM(NOTA_PESO) AS NOTAS_EMITIDAS,
                SUM(VALOR_VENDA) AS REALIZADO,
                SUM(VALOR_VENDA - CUSTO - IMPOSTOS - DESPESAS) AS MARGEM_CONTRIBUICAO
            FROM VENDAS
            GROUP BY ANO, MES
            ORDER BY ANO, MES";

        public ECommerceService(IConfiguration configuration, AppDbContext context)
        {
            _context = context;
            _connectionString = GetOracleConnectionString(configuration);
        }

        public async Task<ECommerceDashboardDto> LoadAsync(ECommerceFilterDto filter)
        {
            EnsureConnectionString();

            var dataInicio = (filter.DataInicio ?? DateTime.Today.AddDays(1 - DateTime.Today.Day)).Date;
            var dataFim = (filter.DataFim ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            if (dataInicio > dataFim)
                throw new InvalidOperationException("Data inicial nao pode ser maior que a data final.");

            var unidades = await _context.Unidade
                .AsNoTracking()
                .Where(item => item.EmpresaCadastro != null && item.EmpresaCadastro.Numero > 0 && item.NumeroRevenda > 0)
                .Select(item => new RegisteredUnit(
                    item.EmpresaCadastro == null ? 0 : item.EmpresaCadastro.Numero,
                    item.NumeroRevenda,
                    item.Revenda,
                    item.EmpresaCadastro == null ? item.Empresa : item.EmpresaCadastro.Nome))
                .ToListAsync();

            var empresasCadastradas = unidades.Select(item => item.EmpresaNumero).Distinct().ToHashSet();
            var revendasCadastradas = unidades.Select(item => $"{item.EmpresaNumero}:{item.NumeroRevenda}").Distinct().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var empresa = ApplyRegisteredCompanies(NormalizeFilter(filter.Empresa), empresasCadastradas);
            var revenda = ApplyRegisteredResales(NormalizeFilter(filter.Revenda), revendasCadastradas);

            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var items = await LoadUnitsAsync(connection, dataInicio, dataFim, empresa, revenda, unidades);
            var annualStart = new DateTime(Math.Max(2000, dataFim.Year - 4), 1, 1);
            var annual = await LoadAnnualAsync(connection, annualStart, dataFim, empresa, revenda);
            var monthly = await LoadMonthlyAsync(connection, annualStart, dataFim, empresa, revenda);

            return new ECommerceDashboardDto
            {
                AtualizadoEm = DateTime.UtcNow,
                Unidades = items,
                EvolucaoAnual = annual,
                EvolucaoMensal = monthly,
            };
        }

        private static async Task<List<ECommerceUnitDto>> LoadUnitsAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, string? empresa, string? revenda, IEnumerable<RegisteredUnit> unidades)
        {
            await using var command = new OracleCommand(DashboardSql, connection)
            {
                BindByName = true,
                CommandType = CommandType.Text,
            };
            command.Parameters.Add("DATA_INICIO", OracleDbType.Date, dataInicio, ParameterDirection.Input);
            command.Parameters.Add("DATA_FIM", OracleDbType.Date, dataFim, ParameterDirection.Input);
            command.Parameters.Add("EMPRESA", OracleDbType.Varchar2, string.IsNullOrWhiteSpace(empresa) ? DBNull.Value : empresa, ParameterDirection.Input);
            command.Parameters.Add("REVENDA", OracleDbType.Varchar2, string.IsNullOrWhiteSpace(revenda) ? DBNull.Value : revenda, ParameterDirection.Input);

            var unidadeMap = unidades.ToDictionary(item => $"{item.EmpresaNumero}:{item.NumeroRevenda}", StringComparer.OrdinalIgnoreCase);
            var items = new List<ECommerceUnitDto>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var empresaNumero = Convert.ToInt32(GetDecimal(reader, "EMPRESA"));
                var revendaNumero = Convert.ToInt32(GetDecimal(reader, "REVENDA"));
                var key = $"{empresaNumero}:{revendaNumero}";
                if (!unidadeMap.TryGetValue(key, out var unidade))
                    continue;

                var realizado = GetDecimal(reader, "REALIZADO");
                var notas = Convert.ToInt32(GetDecimal(reader, "NOTAS_EMITIDAS"));
                var margemValor = GetDecimal(reader, "MARGEM_CONTRIBUICAO");
                var vendedorCodigo = GetNullableInt(reader, "VENDEDOR");
                var vendedorNome = GetString(reader, "NOME_VENDEDOR");

                items.Add(new ECommerceUnitDto
                {
                    EmpresaNumero = empresaNumero,
                    RevendaNumero = revendaNumero,
                    VendedorCodigo = vendedorCodigo,
                    VendedorNome = vendedorNome,
                    Nome = string.IsNullOrWhiteSpace(unidade.Nome) ? $"{unidade.Empresa} {revendaNumero}" : unidade.Nome,
                    NomeCurto = $"{empresaNumero}.{revendaNumero}",
                    Realizado = realizado,
                    NotasEmitidas = notas,
                    TicketMedio = notas > 0 ? realizado / notas : 0,
                    Custo = GetDecimal(reader, "CUSTO"),
                    Impostos = GetDecimal(reader, "IMPOSTOS"),
                    Despesas = GetDecimal(reader, "DESPESAS"),
                    MargemContribuicaoValor = margemValor,
                    MargemContribuicaoPercentual = realizado != 0 ? margemValor / realizado : 0,
                });
            }

            return items;
        }

        private static async Task<List<ECommerceAnnualSaleDto>> LoadAnnualAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, string? empresa, string? revenda)
        {
            await using var command = new OracleCommand(AnnualSql, connection)
            {
                BindByName = true,
                CommandType = CommandType.Text,
            };
            command.Parameters.Add("DATA_INICIO", OracleDbType.Date, dataInicio, ParameterDirection.Input);
            command.Parameters.Add("DATA_FIM", OracleDbType.Date, dataFim, ParameterDirection.Input);
            command.Parameters.Add("EMPRESA", OracleDbType.Varchar2, string.IsNullOrWhiteSpace(empresa) ? DBNull.Value : empresa, ParameterDirection.Input);
            command.Parameters.Add("REVENDA", OracleDbType.Varchar2, string.IsNullOrWhiteSpace(revenda) ? DBNull.Value : revenda, ParameterDirection.Input);

            var items = new List<ECommerceAnnualSaleDto>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var realizado = GetDecimal(reader, "REALIZADO");
                var margem = GetDecimal(reader, "MARGEM_CONTRIBUICAO");
                items.Add(new ECommerceAnnualSaleDto
                {
                    Ano = Convert.ToInt32(GetDecimal(reader, "ANO")),
                    Realizado = realizado,
                    NotasEmitidas = Convert.ToInt32(GetDecimal(reader, "NOTAS_EMITIDAS")),
                    MargemContribuicaoValor = margem,
                    MargemContribuicaoPercentual = realizado != 0 ? margem / realizado : 0,
                });
            }

            return items;
        }

        private static async Task<List<ECommerceMonthlySaleDto>> LoadMonthlyAsync(OracleConnection connection, DateTime dataInicio, DateTime dataFim, string? empresa, string? revenda)
        {
            await using var command = new OracleCommand(MonthlySql, connection)
            {
                BindByName = true,
                CommandType = CommandType.Text,
            };
            command.Parameters.Add("DATA_INICIO", OracleDbType.Date, dataInicio, ParameterDirection.Input);
            command.Parameters.Add("DATA_FIM", OracleDbType.Date, dataFim, ParameterDirection.Input);
            command.Parameters.Add("EMPRESA", OracleDbType.Varchar2, string.IsNullOrWhiteSpace(empresa) ? DBNull.Value : empresa, ParameterDirection.Input);
            command.Parameters.Add("REVENDA", OracleDbType.Varchar2, string.IsNullOrWhiteSpace(revenda) ? DBNull.Value : revenda, ParameterDirection.Input);

            var items = new List<ECommerceMonthlySaleDto>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var realizado = GetDecimal(reader, "REALIZADO");
                var margem = GetDecimal(reader, "MARGEM_CONTRIBUICAO");
                items.Add(new ECommerceMonthlySaleDto
                {
                    Ano = Convert.ToInt32(GetDecimal(reader, "ANO")),
                    Mes = Convert.ToInt32(GetDecimal(reader, "MES")),
                    Realizado = realizado,
                    NotasEmitidas = Convert.ToInt32(GetDecimal(reader, "NOTAS_EMITIDAS")),
                    MargemContribuicaoValor = margem,
                    MargemContribuicaoPercentual = realizado != 0 ? margem / realizado : 0,
                });
            }

            return items;
        }

        private static string? NormalizeFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var items = value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return items.Length == 0 ? null : string.Join(',', items);
        }

        private static string? ApplyRegisteredCompanies(string? empresa, HashSet<int> empresasCadastradas)
        {
            var requested = SplitFilter(empresa)
                .Select(item => int.TryParse(item, out var numero) ? numero : 0)
                .Where(numero => numero > 0)
                .ToHashSet();
            var allowed = requested.Count > 0
                ? empresasCadastradas.Where(requested.Contains)
                : empresasCadastradas;

            return string.Join(',', allowed.OrderBy(item => item));
        }

        private static string? ApplyRegisteredResales(string? revenda, HashSet<string> revendasCadastradas)
        {
            var requested = SplitFilter(revenda).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var allowed = requested.Count > 0
                ? revendasCadastradas.Where(requested.Contains)
                : revendasCadastradas;

            return string.Join(',', allowed.OrderBy(item => item, StringComparer.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> SplitFilter(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Array.Empty<string>()
                : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private void EnsureConnectionString()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string Oracle nao configurada para o dashboard de e-commerce.");
        }

        private static string GetOracleConnectionString(IConfiguration configuration)
        {
            var environment = configuration["Oracle:Environment"]?.Trim();
            var preferredName = string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase)
                ? "OracleConnectionProduction"
                : "OracleConnectionDve";

            return configuration.GetConnectionString(preferredName)
                ?? configuration.GetConnectionString("OracleConnection")
                ?? string.Empty;
        }

        private static decimal GetDecimal(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal))
                return 0;

            if (reader is OracleDataReader oracleReader)
            {
                var value = oracleReader.GetOracleDecimal(ordinal);
                return value.IsNull ? 0 : value.Value;
            }

            var rawValue = reader.GetValue(ordinal);
            return rawValue switch
            {
                decimal decimalValue => decimalValue,
                OracleDecimal oracleDecimal => oracleDecimal.IsNull ? 0 : oracleDecimal.Value,
                int intValue => intValue,
                long longValue => longValue,
                double doubleValue => Convert.ToDecimal(doubleValue),
                _ => Convert.ToDecimal(rawValue),
            };
        }

        private static int? GetNullableInt(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal))
                return null;

            return Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static string GetString(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
        }
    }
}
