using System.Security.Claims;
using DRFlowHub.Api.Dtos.Unidades;
using DRFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DRFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class UnidadesController : ControllerBase
    {
        private readonly UnidadesService _service;

        public UnidadesController(UnidadesService service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult List()
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            return Ok(_service.List(role));
        }

        [HttpGet("empresas")]
        public IActionResult ListEmpresas()
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            return Ok(_service.ListEmpresas(role));
        }

        [HttpPost("empresas")]
        public IActionResult AddEmpresa([FromBody] EmpresaCreateDto dto)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                return Ok(_service.AddEmpresa(dto, role));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("empresas/{id:int}")]
        public IActionResult UpdateEmpresa(int id, [FromBody] EmpresaCreateDto dto)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                return Ok(_service.UpdateEmpresa(id, dto, role));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPost]
        public IActionResult Add([FromBody] UnidadeCreateDto dto)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                return Ok(_service.Add(dto, role));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        public IActionResult Update(int id, [FromBody] UnidadeCreateDto dto)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                return Ok(_service.Update(id, dto, role));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
