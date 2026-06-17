using UniFlowHub.Api.Dtos.Veiculos;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class VeiculosController : ControllerBase
    {
        private readonly VeiculosService _service;

        public VeiculosController(VeiculosService service)
        {
            _service = service;
        }

        [HttpGet("estoque")]
        public async Task<IActionResult> ListEstoque([FromQuery] VeiculoEstoqueFilterDto filter)
        {
            try
            {
                return Ok(await _service.ListEstoqueAsync(GetRole(), filter));
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

        [HttpPost("estoque/{chassi}/reserva")]
        public async Task<IActionResult> UpdateReserva(string chassi, [FromBody] VeiculoReservaUpdateDto dto)
        {
            try
            {
                return Ok(await _service.UpdateReservaAsync(chassi, dto, GetRole(), GetCurrentUserId()));
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
