using UniFlowHub.Api.Dtos.Controladoria;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ControladoriaController : ControllerBase
    {
        private readonly ControladoriaService _service;

        public ControladoriaController(ControladoriaService service)
        {
            _service = service;
        }

        [HttpGet("icms-guias")]
        public async Task<IActionResult> ListGuiasIcms([FromQuery] GuiaIcmsFilterDto filter)
        {
            try
            {
                return Ok(await _service.ListGuiasIcmsAsync(GetRole(), filter));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("icms-guias/{id}/pagamento")]
        public async Task<IActionResult> UpdateGuiaIcmsPagamento(string id, [FromBody] GuiaIcmsPagamentoUpdateDto dto)
        {
            try
            {
                return Ok(await _service.UpdateGuiaIcmsPagamentoAsync(id, dto, GetRole(), GetCurrentUserId()));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("icms-guias/pagamento-lote")]
        public async Task<IActionResult> UpdateGuiaIcmsPagamentoLote([FromBody] GuiaIcmsPagamentoLoteDto dto)
        {
            try
            {
                return Ok(await _service.UpdateGuiaIcmsPagamentoLoteAsync(dto, GetRole(), GetCurrentUserId()));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private string GetRole()
        {
            return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        }

        private int GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(value, out var userId))
                return userId;

            throw new UnauthorizedAccessException("Usuario invalido.");
        }
    }
}
