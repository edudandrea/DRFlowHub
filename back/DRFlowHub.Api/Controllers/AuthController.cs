using System.Security.Claims;
using DRFlowHub.Api.Dtos;
using DRFlowHub.Api.Dtos.Auth;
using DRFlowHub.Api.Security;
using DRFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DRFlowHub.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _service;

        public AuthController(AuthService service)
        {
            _service = service;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public IActionResult Login([FromBody] LoginRequestDto dto)
        {
            try
            {
                return Ok(_service.Login(dto));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public IActionResult Register([FromBody] UserCreateDto dto)
        {
            var hasAnyUser = _service.HasAnyUser();
            if (hasAnyUser && !UserHasRole("Admin") && !UserHasRole("TI"))
                return Forbid();

            if (!hasAnyUser)
            {
                dto.Role = "Admin";
                dto.UnidadeId = null;
                dto.Ativo = true;
            }

            var currentUserId = GetCurrentUserId();
            var user = _service.CreateUser(dto, hasAnyUser ? currentUserId : null);
            return Ok(user);
        }

        [HttpGet("setup-status")]
        [AllowAnonymous]
        public IActionResult SetupStatus()
        {
            return Ok(new
            {
                canCreateFirstAdmin = !_service.HasAnyUser()
            });
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult Me()
        {
            return Ok(new
            {
                id = GetCurrentUserId(),
                nome = User.Identity?.Name,
                email = User.FindFirstValue(ClaimTypes.Email),
                role = User.FindFirstValue(ClaimTypes.Role)
            });
        }

        private bool UserHasRole(string role)
        {
            return string.Equals(User.FindFirstValue(ClaimTypes.Role), role, StringComparison.OrdinalIgnoreCase);
        }

        private int? GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var userId) ? userId : null;
        }
    }
}
