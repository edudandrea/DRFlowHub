using System.Security.Claims;
using UniFlowHub.Api.Dtos.Unidades;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class UnidadesController : ControllerBase
    {
        private readonly UnidadesService _service;
        private readonly OracleEmpresasService _oracleService;

        public UnidadesController(UnidadesService service, OracleEmpresasService oracleService)
        {
            _service = service;
            _oracleService = oracleService;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            return Ok(await _oracleService.ListUnidadesAsync(role));
        }

        /// <summary>
        /// Lista empresas e revendas do Oracle com informações de montadora e logo do PostgreSQL
        /// Endpoint READ-ONLY - sem suporte para criação ou atualização de dados
        /// </summary>
        [HttpGet("empresas-revendas")]
        public async Task<IActionResult> ListEmpresasRevendasFromOracle([FromQuery] bool includeInativas = false)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                var result = await _oracleService.ListRevendasAsync(role, includeInativas);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Lista apenas empresas do PostgreSQL (DEPRECATED - será removido quando migração terminar)
        /// </summary>
        [HttpGet("empresas")]
        public async Task<IActionResult> ListEmpresas()
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            return Ok(await _oracleService.ListEmpresasAsync(role));
        }

        /// <summary>
        /// Adicionar unidade (revenda) - APENAS PARA DADOS LEGADOS, usar OracleEmpresasService
        /// </summary>
        [HttpPost]
        public IActionResult Add([FromBody] UnidadeCreateDto dto)
        {
            try
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed, "Cadastro de revendas e feito somente no Oracle.");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Atualizar unidade (revenda) - APENAS PARA DADOS LEGADOS
        /// </summary>
        [HttpPut("{id:int}")]
        public IActionResult Update(int id, [FromBody] UnidadeCreateDto dto)
        {
            try
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed, "Cadastro de revendas e feito somente no Oracle.");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Atualizar montadora e logo de uma revenda do Oracle
        /// </summary>
        [HttpPut("empresas-revendas/{empresaNumero:int}/{revendaNumero:int}/montadora")]
        public async Task<IActionResult> UpdateMontadora(
            int empresaNumero, 
            int revendaNumero, 
            [FromBody] UpdateMontadoraDto dto)
        {
            try
            {
                // Verificar permissão
                var acessos = GetAcessos();
                if (!acessos.Contains("empresas-revendas", StringComparer.OrdinalIgnoreCase) &&
                    !User.IsInRole("Admin") && !User.IsInRole("TI"))
                {
                    return Forbid();
                }

                var result = await _oracleService.UpdateMontadoraAsync(
                    empresaNumero, 
                    revendaNumero, 
                    dto.Montadora, 
                    dto.LogoMontadoraUrl);

                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("empresas-revendas/{empresaNumero:int}/status")]
        public async Task<IActionResult> UpdateEmpresaStatus(int empresaNumero, [FromBody] UpdateStatusDto dto)
        {
            try
            {
                EnsureCanManage();
                return Ok(await _oracleService.UpdateEmpresaStatusAsync(empresaNumero, dto.Ativa));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("empresas-revendas/{empresaNumero:int}/{revendaNumero:int}/status")]
        public async Task<IActionResult> UpdateRevendaStatus(int empresaNumero, int revendaNumero, [FromBody] UpdateStatusDto dto)
        {
            try
            {
                EnsureCanManage();
                return Ok(await _oracleService.UpdateRevendaStatusAsync(empresaNumero, revendaNumero, dto.Ativa));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private IEnumerable<string> GetAcessos()
        {
            return User.FindAll("access").Select(claim => claim.Value);
        }

        private void EnsureCanManage()
        {
            var acessos = GetAcessos();
            if (!acessos.Contains("empresas-revendas", StringComparer.OrdinalIgnoreCase) &&
                !User.IsInRole("Admin") && !User.IsInRole("TI"))
            {
                throw new UnauthorizedAccessException();
            }
        }
    }
}
