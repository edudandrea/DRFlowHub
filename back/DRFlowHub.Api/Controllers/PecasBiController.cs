using DRFlowHub.Api.Dtos.PecasBi;
using DRFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DRFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/pecas-bi")]
    public class PecasBiController : ControllerBase
    {
        private readonly PecasBiService _service;

        public PecasBiController(PecasBiService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Load([FromQuery] PecasBiFilterDto filter)
        {
            try
            {
                return Ok(await _service.LoadAsync(GetRole(), GetCurrentUserId(), filter));
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

        [HttpPut("vendedores/meta")]
        public async Task<IActionResult> SaveMeta([FromBody] PecaVendedorMetaDto dto)
        {
            try
            {
                return Ok(await _service.SaveMetaAsync(GetRole(), GetCurrentUserId(), dto));
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
