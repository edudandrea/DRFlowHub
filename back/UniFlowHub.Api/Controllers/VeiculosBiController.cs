using System.Security.Claims;
using UniFlowHub.Api.Dtos.VeiculosBi;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/veiculos-bi")]
    public class VeiculosBiController : ControllerBase
    {
        private readonly VeiculosBiService _service;

        public VeiculosBiController(VeiculosBiService service)
        {
            _service = service;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard([FromQuery] VeiculosBiFilterDto filter)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                return Ok(await _service.LoadDashboardAsync(role, filter));
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ex.Message); }
        }

        [HttpGet("acessorios")]
        public async Task<IActionResult> Acessorios([FromQuery] VeiculosBiFilterDto filter)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                return Ok(await _service.LoadAcessoriosAsync(role, filter));
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ex.Message); }
        }

        [HttpGet("retorno-fi")]
        public async Task<IActionResult> RetornoFi([FromQuery] VeiculosBiFilterDto filter)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                return Ok(await _service.LoadRetornoFiAsync(role, filter));
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ex.Message); }
        }
    }
}
