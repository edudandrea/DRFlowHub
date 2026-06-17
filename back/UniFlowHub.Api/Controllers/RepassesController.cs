using System.Security.Claims;
using UniFlowHub.Api.Dtos.Repasses;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class RepassesController : ControllerBase
    {
        private readonly RepassesService _service;

        public RepassesController(RepassesService service)
        {
            _service = service;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard([FromQuery] RepasseDashboardFilterDto filter)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                var acessos = User.FindAll("access").Select(claim => claim.Value);
                return Ok(await _service.GetDashboardAsync(role, acessos, filter));
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
    }
}
