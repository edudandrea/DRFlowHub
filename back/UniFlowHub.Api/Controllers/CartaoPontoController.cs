using System.Security.Claims;
using UniFlowHub.Api.Dtos.CartaoPonto;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class CartaoPontoController : ControllerBase
    {
        private readonly CartaoPontoService _service;

        public CartaoPontoController(CartaoPontoService service)
        {
            _service = service;
        }

        [HttpGet("arquivos")]
        public IActionResult ListArquivos()
        {
            return Ok(_service.ListArquivos(GetRole(), GetUserId()));
        }

        [HttpPost("importar")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Importar([FromForm] IFormFile arquivo)
        {
            try
            {
                var result = await _service.Importar(arquivo, GetRole(), GetUserId());
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("funcionarios")]
        public IActionResult ListFuncionarios([FromQuery] int? arquivoId)
        {
            return Ok(_service.ListFuncionarios(arquivoId, GetRole(), GetUserId()));
        }

        [HttpGet("funcionarios/{cpf}/registros")]
        public IActionResult ListRegistros(string cpf, [FromQuery] int? arquivoId)
        {
            return Ok(_service.ListRegistros(cpf, arquivoId, GetRole(), GetUserId()));
        }

        [HttpPut("registros/{id:int}")]
        public IActionResult UpdateRegistro(int id, [FromBody] CartaoPontoRegistroUpdateDto dto)
        {
            try
            {
                return Ok(_service.UpdateRegistro(id, dto, GetRole(), GetUserId()));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("funcionarios/{cpf}/resposta")]
        public IActionResult ResponderFuncionario(string cpf, [FromQuery] int? arquivoId, [FromQuery] string? mes, [FromBody] CartaoPontoRespostaUsuarioDto dto)
        {
            _service.ResponderFuncionario(cpf, arquivoId, mes, dto, GetRole(), GetUserId());
            return Ok(new { sucesso = true });
        }

        private int GetUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(value, out var userId))
                return userId;

            throw new UnauthorizedAccessException("Usuario invalido.");
        }

        private string GetRole()
        {
            return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        }
    }
}
