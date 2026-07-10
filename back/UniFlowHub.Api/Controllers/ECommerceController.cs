using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniFlowHub.Api.Dtos.ECommerce;
using UniFlowHub.Api.Services;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/e-commerce")]
    public class ECommerceController : ControllerBase
    {
        private readonly ECommerceService _service;

        public ECommerceController(ECommerceService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard([FromQuery] ECommerceFilterDto filter)
        {
            try
            {
                return Ok(await _service.LoadAsync(filter));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("importar-planilha")]
        public async Task<IActionResult> ImportarPlanilha([FromForm] IFormFile arquivo)
        {
            try
            {
                return Ok(await _service.ImportSpreadsheetAsync(arquivo));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
