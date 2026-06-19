using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniFlowHub.Api.Dtos.GestaoPessoas;
using UniFlowHub.Api.Services;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class GestaoPessoasController : ControllerBase
    {
        private readonly GestaoPessoasService _service;

        public GestaoPessoasController(GestaoPessoasService service)
        {
            _service = service;
        }

        [HttpGet("etapas")]
        public async Task<IActionResult> ListEtapas([FromQuery] string? tipoProcesso)
        {
            return Ok(await _service.ListEtapasAsync(tipoProcesso));
        }

        [HttpPost("etapas")]
        public async Task<IActionResult> CreateEtapa([FromBody] GestaoPessoasEtapaSaveDto dto)
        {
            try
            {
                var etapa = await _service.SaveEtapaAsync(null, dto, GetRole(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Etapa cadastrada com sucesso", etapa });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("etapas/{id:int}")]
        public async Task<IActionResult> UpdateEtapa(int id, [FromBody] GestaoPessoasEtapaSaveDto dto)
        {
            try
            {
                var etapa = await _service.SaveEtapaAsync(id, dto, GetRole(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Etapa atualizada com sucesso", etapa });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("etapas/{id:int}")]
        public async Task<IActionResult> DeleteEtapa(int id)
        {
            try
            {
                await _service.DeleteEtapaAsync(id, GetRole(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Etapa removida com sucesso" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("processos")]
        public async Task<IActionResult> ListProcessos()
        {
            return Ok(await _service.ListProcessosAsync(GetRole(), GetUserId(), GetAcessos()));
        }

        [HttpPost("processos")]
        public async Task<IActionResult> CreateProcesso([FromBody] GestaoPessoasProcessoCreateDto dto)
        {
            try
            {
                var processo = await _service.CreateProcessoAsync(dto, GetRole(), GetUserId(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Processo criado com sucesso", processo });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("processos/{id:int}/avancar")]
        public async Task<IActionResult> Avancar(int id, [FromBody] GestaoPessoasMovimentoDto dto)
        {
            try
            {
                var processo = await _service.AvancarAsync(id, dto, GetRole(), GetUserId(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Etapa avancada com sucesso", processo });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("processos/{id:int}/voltar")]
        public async Task<IActionResult> Voltar(int id, [FromBody] GestaoPessoasMovimentoDto dto)
        {
            try
            {
                var processo = await _service.VoltarAsync(id, dto, GetRole(), GetUserId(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Processo retornado para etapa anterior", processo });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("processos/{id:int}/cancelar")]
        public async Task<IActionResult> Cancelar(int id, [FromBody] GestaoPessoasCancelamentoDto dto)
        {
            try
            {
                var processo = await _service.CancelarAsync(id, dto, GetRole(), GetUserId(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Processo cancelado com sucesso", processo });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
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

        private IEnumerable<string> GetAcessos()
        {
            return User.FindAll("access").Select(claim => claim.Value);
        }
    }
}
