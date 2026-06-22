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

        [HttpGet("cargos")]
        public async Task<IActionResult> ListCargos()
        {
            return Ok(await _service.ListCargosAsync());
        }

        [HttpPost("cargos")]
        public async Task<IActionResult> CreateCargo([FromBody] GestaoPessoasCargoSaveDto dto)
        {
            try
            {
                var cargo = await _service.SaveCargoAsync(null, dto, GetRole(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Cargo cadastrado com sucesso", cargo });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("cargos/{id:int}")]
        public async Task<IActionResult> UpdateCargo(int id, [FromBody] GestaoPessoasCargoSaveDto dto)
        {
            try
            {
                var cargo = await _service.SaveCargoAsync(id, dto, GetRole(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Cargo atualizado com sucesso", cargo });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("itens")]
        public async Task<IActionResult> ListItens([FromQuery] string? tipo)
        {
            return Ok(await _service.ListItensAsync(tipo));
        }

        [HttpPost("itens")]
        public async Task<IActionResult> CreateItem([FromBody] GestaoPessoasItemSaveDto dto)
        {
            try
            {
                var item = await _service.SaveItemAsync(null, dto, GetRole(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Item cadastrado com sucesso", item });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("itens/{id:int}")]
        public async Task<IActionResult> UpdateItem(int id, [FromBody] GestaoPessoasItemSaveDto dto)
        {
            try
            {
                var item = await _service.SaveItemAsync(id, dto, GetRole(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Item atualizado com sucesso", item });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("colaboradores")]
        public async Task<IActionResult> ListColaboradores()
        {
            return Ok(await _service.ListColaboradoresAsync());
        }

        [HttpPost("colaboradores")]
        public async Task<IActionResult> CreateColaborador([FromBody] GestaoPessoasColaboradorSaveDto dto)
        {
            try
            {
                var colaborador = await _service.SaveColaboradorAsync(null, dto, GetRole(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Colaborador cadastrado com sucesso", colaborador });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("colaboradores/{id:int}")]
        public async Task<IActionResult> UpdateColaborador(int id, [FromBody] GestaoPessoasColaboradorSaveDto dto)
        {
            try
            {
                var colaborador = await _service.SaveColaboradorAsync(id, dto, GetRole(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Colaborador atualizado com sucesso", colaborador });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("colaboradores/{id:int}/retiradas")]
        public async Task<IActionResult> AddRetirada(int id, [FromBody] GestaoPessoasColaboradorRetiradaSaveDto dto)
        {
            try
            {
                var retirada = await _service.AddRetiradaAsync(id, dto, GetRole(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Retirada cadastrada com sucesso", retirada });
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
