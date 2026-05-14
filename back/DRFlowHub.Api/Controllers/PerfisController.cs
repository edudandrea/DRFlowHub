using System.Security.Claims;
using DRFlowHub.Api.Dtos.Perfis;
using DRFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DRFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/perfis")]
    public class PerfisController : ControllerBase
    {
        private readonly PerfisService _service;

        public PerfisController(PerfisService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            try { return Ok(await _service.ListAsync(GetRole())); }
            catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ex.Message); }
        }

        [HttpGet("acessos")]
        public async Task<IActionResult> ListAcessos()
        {
            return Ok(await _service.ListAcessosAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] PerfilSistemaSaveDto dto)
        {
            try { return Ok(await _service.SaveAsync(GetRole(), dto)); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ex.Message); }
        }

        private string GetRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }
}
