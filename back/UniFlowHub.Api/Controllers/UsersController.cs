using System.Security.Claims;
using UniFlowHub.Api.Dtos;
using UniFlowHub.Api.Security;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly UsersService _service;

        public UsersController(UsersService service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult List()
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            return Ok(_service.List(role, email, GetAcessos()));
        }

        [HttpGet("administradores")]
        public IActionResult ListAdministradores()
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            return Ok(_service.ListAdministradores(role, GetAcessos()));
        }

        [HttpPut("{id:int}")]
        public IActionResult Update(int id, [FromBody] UserUpdateDto dto)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                return Ok(_service.Update(id, dto, role, GetAcessos()));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("{id:int}/update")]
        public IActionResult UpdateViaPost(int id, [FromBody] UserUpdateDto dto)
        {
            return Update(id, dto);
        }

        [HttpPut("me")]
        public IActionResult UpdateProfile([FromBody] UserProfileUpdateDto dto)
        {
            try
            {
                return Ok(_service.UpdateProfile(GetCurrentUserId(), dto));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("me/password")]
        public IActionResult ChangePassword([FromBody] UserChangePasswordDto dto)
        {
            try
            {
                _service.ChangePassword(GetCurrentUserId(), dto);
                return Ok(new { sucesso = true, mensagem = "Senha alterada com sucesso." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        private int GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(value, out var userId))
                return userId;

            throw new UnauthorizedAccessException("Usuario invalido.");
        }

        private IEnumerable<string> GetAcessos()
        {
            return User.FindAll("access").Select(claim => claim.Value);
        }
    }
}
