using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Globalization;
using System.Data;
using System.Data.Common;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
                    CASE WHEN FMC.TIPO_TRANSACAO = 'P77' THEN -ABS(SUM(
                        COALESCE(FMI.VAL_ICMS_PARTIL_UF_DEST, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_REM, 0)
                        + COALESCE(FMI.DIFERENCA_ICMS_REDUZIDO, 0)
                    )) ELSE SUM(
                        COALESCE(FMI.VAL_ICMS_PARTIL_UF_DEST, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_REM, 0)
                        + COALESCE(FMI.DIFERENCA_ICMS_REDUZIDO, 0)
                    ) END AS ICMS_DIFAL,
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
                    -ABS(SUM(
                        COALESCE(FMI.VAL_ICMS_PARTIL_UF_DEST, 0)
                        + COALESCE(FMI.VAL_ICMS_PARTIL_UF_REM, 0)
                        + COALESCE(FMI.DIFERENCA_ICMS_REDUZIDO, 0)
                    )) AS ICMS_DIFAL,
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
                SUM(VALOR_VENDA - CUSTO - IMPOSTOS - DESPESAS) AS MARGEM_CONTRIBUICAO,
                SUM(VALOR_VENDA - CUSTO - IMPOSTOS) AS RENTABILIDADE_DMS
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

        public async Task<ECommerceSpreadsheetImportDto> ImportSpreadsheetAsync(IFormFile arquivo)
        {
            if (arquivo == null || arquivo.Length == 0)
                throw new InvalidOperationException("Selecione uma planilha de e-commerce para importar.");

            var extension = Path.GetExtension(arquivo.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Envie uma planilha no formato .xlsx.");

            await using var stream = arquivo.OpenReadStream();
            var rows = ReadXlsxRows(stream);
            var headerIndex = rows.FindIndex(IsECommerceHeaderRow);
            if (headerIndex < 0)
                throw new InvalidOperationException("Nao foi possivel localizar as colunas Receita por produtos (BRL) e Total (BRL).");

            var header = rows[headerIndex];
            var revenueColumn = FindHeaderColumn(header, "receitaporprodutos");
            var totalColumn = FindHeaderColumn(header, "totalbrl");
            if (revenueColumn == null || totalColumn == null)
                throw new InvalidOperationException("Nao foi possivel localizar as colunas Receita por produtos (BRL) e Total (BRL).");

            var saleColumn = FindHeaderColumn(header, "ndevenda") ?? FindHeaderColumn(header, "venda");
            var skuColumn = FindHeaderColumn(header, "sku");
            var titleColumn = FindHeaderColumn(header, "titulodoanuncio");
            var dateColumn = FindHeaderColumn(header, "datadavenda");
            var channelColumn = FindHeaderColumn(header, "canaldevenda");

            var importedRows = rows
                .Skip(headerIndex + 1)
                .Select(row => ToSpreadsheetRow(row, revenueColumn.Value, totalColumn.Value, saleColumn, skuColumn, titleColumn, dateColumn, channelColumn))
                .Where(row => row != null)
                .Cast<SpreadsheetSaleRow>()
                .ToList();

            if (importedRows.Count == 0)
                throw new InvalidOperationException("Nenhuma linha valida foi encontrada na planilha.");

            var margemContribuicao = importedRows.Sum(row => (row.ProductRevenue - row.Total) / 2);

            var units = importedRows
                .GroupBy(row => row.GroupKey, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Sum(row => row.Total))
                .Select((group, index) =>
                {
                    var total = group.Sum(row => row.Total);
                    var revenue = group.Sum(row => row.ProductRevenue);
                    var margin = (revenue - total) / 2;
                    var invoiceCount = group.Select(row => row.SaleId).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    if (invoiceCount == 0)
                        invoiceCount = group.Count();

                    return new ECommerceUnitDto
                    {
                        EmpresaNumero = 0,
                        RevendaNumero = index + 1,
                        VendedorCodigo = null,
                        VendedorNome = group.First().Channel,
                        Nome = group.First().DisplayName,
                        NomeCurto = Truncate(group.First().ShortName, 28),
                        Realizado = total,
                        NotasEmitidas = invoiceCount,
                        TicketMedio = invoiceCount > 0 ? total / invoiceCount : 0,
                        Custo = 0,
                        Impostos = 0,
                        Despesas = revenue - total,
                        MargemContribuicaoValor = margin,
                        MargemContribuicaoPercentual = total != 0 ? margin / total : 0,
                    };
                })
                .ToList();

            var annual = importedRows
                .Where(row => row.SaleDate.HasValue)
                .GroupBy(row => row.SaleDate!.Value.Year)
                .OrderBy(group => group.Key)
                .Select(group =>
                {
                    var total = group.Sum(row => row.Total);
                    var margin = group.Sum(row => (row.ProductRevenue - row.Total) / 2);
                    return new ECommerceAnnualSaleDto
                    {
                        Ano = group.Key,
                        Realizado = total,
                        NotasEmitidas = group.Count(),
                        MargemContribuicaoValor = margin,
                        MargemContribuicaoPercentual = total != 0 ? margin / total : 0,
                    };
                })
                .ToList();

            var monthly = importedRows
                .Where(row => row.SaleDate.HasValue)
                .GroupBy(row => new { row.SaleDate!.Value.Year, row.SaleDate.Value.Month })
                .OrderBy(group => group.Key.Year)
                .ThenBy(group => group.Key.Month)
                .Select(group =>
                {
                    var total = group.Sum(row => row.Total);
                    var margin = group.Sum(row => (row.ProductRevenue - row.Total) / 2);
                    return new ECommerceMonthlySaleDto
                    {
                        Ano = group.Key.Year,
                        Mes = group.Key.Month,
                        Realizado = total,
                        NotasEmitidas = group.Count(),
                        MargemContribuicaoValor = margin,
                        MargemContribuicaoPercentual = total != 0 ? margin / total : 0,
                    };
                })
                .ToList();

            return new ECommerceSpreadsheetImportDto
            {
                LinhasImportadas = importedRows.Count,
                MargemContribuicaoValor = margemContribuicao,
                Dashboard = new ECommerceDashboardDto
                {
                    AtualizadoEm = DateTime.UtcNow,
                    Unidades = units,
                    EvolucaoAnual = annual,
                    EvolucaoMensal = monthly,
                },
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
                var rentabilidadeValor = GetDecimal(reader, "RENTABILIDADE_DMS");
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
                    MargemContribuicaoValor = 0,
                    MargemContribuicaoPercentual = 0,
                    RentabilidadeValor = rentabilidadeValor,
                    RentabilidadePercentual = realizado != 0 ? rentabilidadeValor / realizado : 0,
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
                items.Add(new ECommerceAnnualSaleDto
                {
                    Ano = Convert.ToInt32(GetDecimal(reader, "ANO")),
                    Realizado = realizado,
                    NotasEmitidas = Convert.ToInt32(GetDecimal(reader, "NOTAS_EMITIDAS")),
                    MargemContribuicaoValor = 0,
                    MargemContribuicaoPercentual = 0,
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
                items.Add(new ECommerceMonthlySaleDto
                {
                    Ano = Convert.ToInt32(GetDecimal(reader, "ANO")),
                    Mes = Convert.ToInt32(GetDecimal(reader, "MES")),
                    Realizado = realizado,
                    NotasEmitidas = Convert.ToInt32(GetDecimal(reader, "NOTAS_EMITIDAS")),
                    MargemContribuicaoValor = 0,
                    MargemContribuicaoPercentual = 0,
                });
            }

            return items;
        }

        private sealed record SpreadsheetSaleRow(
            string SaleId,
            string GroupKey,
            string DisplayName,
            string ShortName,
            string Channel,
            DateTime? SaleDate,
            decimal ProductRevenue,
            decimal Total);

        private static List<Dictionary<int, string>> ReadXlsxRows(Stream stream)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var sharedStrings = ReadSharedStrings(archive);
            var worksheet = archive.GetEntry("xl/worksheets/sheet1.xml")
                ?? archive.Entries.FirstOrDefault(entry => entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

            if (worksheet == null)
                throw new InvalidOperationException("Nao foi possivel ler a primeira aba da planilha.");

            using var worksheetStream = worksheet.Open();
            var document = XDocument.Load(worksheetStream);
            var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            var rows = new List<Dictionary<int, string>>();

            foreach (var row in document.Descendants(ns + "sheetData").Elements(ns + "row"))
            {
                var values = new Dictionary<int, string>();
                foreach (var cell in row.Elements(ns + "c"))
                {
                    var reference = cell.Attribute("r")?.Value ?? string.Empty;
                    var column = GetColumnIndex(reference);
                    if (column <= 0)
                        continue;

                    var value = ReadCellValue(cell, sharedStrings, ns);
                    if (!string.IsNullOrWhiteSpace(value))
                        values[column] = value.Trim();
                }

                rows.Add(values);
            }

            return rows;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
                return new List<string>();

            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            return document.Descendants(ns + "si")
                .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
                .ToList();
        }

        private static string ReadCellValue(XElement cell, List<string> sharedStrings, XNamespace ns)
        {
            var type = cell.Attribute("t")?.Value;
            if (type == "inlineStr")
                return string.Concat(cell.Descendants(ns + "t").Select(item => item.Value));

            var rawValue = cell.Element(ns + "v")?.Value ?? string.Empty;
            if (type == "s" && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < sharedStrings.Count)
                return sharedStrings[index];

            return rawValue;
        }

        private static bool IsECommerceHeaderRow(Dictionary<int, string> row)
        {
            return row.Values.Any(value => NormalizeHeader(value).Contains("receitaporprodutos", StringComparison.Ordinal))
                && row.Values.Any(value => NormalizeHeader(value).Equals("totalbrl", StringComparison.Ordinal));
        }

        private static int? FindHeaderColumn(Dictionary<int, string> header, string normalizedName)
        {
            foreach (var item in header)
            {
                var normalized = NormalizeHeader(item.Value);
                if (normalized.Equals(normalizedName, StringComparison.Ordinal) || normalized.Contains(normalizedName, StringComparison.Ordinal))
                    return item.Key;
            }

            return null;
        }

        private static SpreadsheetSaleRow? ToSpreadsheetRow(
            Dictionary<int, string> row,
            int revenueColumn,
            int totalColumn,
            int? saleColumn,
            int? skuColumn,
            int? titleColumn,
            int? dateColumn,
            int? channelColumn)
        {
            var revenue = ReadDecimal(row, revenueColumn);
            var total = ReadDecimal(row, totalColumn);
            if (revenue == 0 && total == 0)
                return null;

            var saleId = ReadText(row, saleColumn);
            var sku = ReadText(row, skuColumn);
            var title = ReadText(row, titleColumn);
            var channel = ReadText(row, channelColumn);
            var displayName = !string.IsNullOrWhiteSpace(title) ? title : !string.IsNullOrWhiteSpace(sku) ? sku : !string.IsNullOrWhiteSpace(saleId) ? $"Venda {saleId}" : "Produto e-commerce";
            var shortName = !string.IsNullOrWhiteSpace(sku) ? sku : displayName;
            var groupKey = !string.IsNullOrWhiteSpace(sku) ? sku : displayName;

            return new SpreadsheetSaleRow(
                saleId,
                groupKey,
                displayName,
                shortName,
                string.IsNullOrWhiteSpace(channel) ? "Planilha e-commerce" : channel,
                TryParseDate(ReadText(row, dateColumn)),
                revenue,
                total);
        }

        private static decimal ReadDecimal(Dictionary<int, string> row, int column)
        {
            if (!row.TryGetValue(column, out var value) || string.IsNullOrWhiteSpace(value))
                return 0;

            value = value.Trim();
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantValue))
                return invariantValue;

            if (decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, new CultureInfo("pt-BR"), out var brazilianValue))
                return brazilianValue;

            var normalized = value.Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var fallbackValue) ? fallbackValue : 0;
        }

        private static string ReadText(Dictionary<int, string> row, int? column)
        {
            return column.HasValue && row.TryGetValue(column.Value, out var value) ? value.Trim() : string.Empty;
        }

        private static DateTime? TryParseDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            value = value.Replace(" hs.", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            var culture = new CultureInfo("pt-BR");
            if (DateTime.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
                return parsed;

            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed) ? parsed : null;
        }

        private static int GetColumnIndex(string cellReference)
        {
            var letters = Regex.Match(cellReference, "^[A-Z]+", RegexOptions.IgnoreCase).Value.ToUpperInvariant();
            var index = 0;
            foreach (var letter in letters)
                index = index * 26 + letter - 'A' + 1;

            return index;
        }

        private static string NormalizeHeader(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
                    builder.Append(char.ToLowerInvariant(character));
            }

            return builder.ToString();
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
                return value;

            return value[..maxLength];
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
