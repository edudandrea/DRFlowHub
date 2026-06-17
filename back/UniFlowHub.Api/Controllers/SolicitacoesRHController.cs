using System.Security.Claims;
using UniFlowHub.Api.Dtos.SolicitacoesRH;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class SolicitacoesRHController : ControllerBase
    {
        private readonly SolicitacoesRHService _service;
        private readonly IWebHostEnvironment _environment;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".doc",
            ".docx",
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".webp"
        };

        public SolicitacoesRHController(SolicitacoesRHService service, IWebHostEnvironment environment)
        {
            _service = service;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult List()
        {
            var solicitacoes = _service.List(GetRole(), GetUserId(), GetAcessos());
            return Ok(solicitacoes);
        }

        [HttpPost]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Add([FromForm] SolicitacoesRHCreateDto dto)
        {
            try
            {
                if (dto.Anexo is not null)
                    dto.AnexossUrl = await SaveAttachment(dto.Anexo);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            var solicitacao = _service.Add(dto, GetRole(), GetUserId(), GetAcessos());

            return Ok(new
            {
                sucesso = true,
                mensagem = "Solicitacao criada com sucesso",
                solicitacao
            });
        }

        [HttpGet("{id:int}/anexo")]
        public IActionResult DownloadAttachment(int id)
        {
            var solicitacao = _service.GetAttachmentOwner(id, GetRole(), GetUserId(), GetAcessos());
            var fullPath = GetAttachmentPath(solicitacao.AnexossUrl);

            if (!System.IO.File.Exists(fullPath))
                return NotFound("Arquivo nao encontrado.");

            var contentType = GetContentType(Path.GetExtension(fullPath));
            var fileName = Path.GetFileName(fullPath);
            return PhysicalFile(fullPath, contentType, fileName, enableRangeProcessing: true);
        }

        [HttpPut("{id:int}")]
        public IActionResult Update(int id, [FromBody] SolicitacoesRHUpdateDto dto)
        {
            try
            {
                var solicitacao = _service.Update(id, dto, GetRole(), GetUserId(), GetAcessos());

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "Solicitacao atualizada com sucesso",
                    solicitacao
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id:int}/comunicacoes")]
        public IActionResult ListComunicacoes(int id)
        {
            return Ok(_service.ListComunicacoes(id, GetRole(), GetUserId(), GetAcessos()));
        }

        [HttpPost("{id:int}/comunicacoes")]
        public IActionResult AddComunicacao(int id, [FromBody] SolicitacaoRHComunicacaoCreateDto dto)
        {
            try
            {
                var comunicacao = _service.AddComunicacao(id, dto, GetRole(), GetUserId(), GetAcessos());

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "Mensagem enviada com sucesso",
                    comunicacao
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id:int}/encerrar")]
        public IActionResult Encerrar(int id, [FromBody] SolicitacoesRHEncerrarDto dto)
        {
            try
            {
                var solicitacao = _service.Encerrar(id, dto, GetRole(), GetUserId(), GetAcessos());

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "Solicitacao encerrada com sucesso",
                    solicitacao
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id:int}/reabrir")]
        public IActionResult Reabrir(int id)
        {
            var solicitacao = _service.Reabrir(id, GetRole(), GetUserId(), GetAcessos());

            return Ok(new
            {
                sucesso = true,
                mensagem = "Solicitacao reaberta com sucesso",
                solicitacao
            });
        }

        [HttpPost("{id:int}/satisfacao")]
        public IActionResult AvaliarSatisfacao(int id, [FromBody] SolicitacoesRHSatisfacaoDto dto)
        {
            try
            {
                var solicitacao = _service.AvaliarSatisfacao(id, dto, GetRole(), GetUserId(), GetAcessos());

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "Avaliacao registrada com sucesso",
                    solicitacao
                });
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

        private async Task<string> SaveAttachment(IFormFile file)
        {
            if (file.Length == 0)
                throw new InvalidOperationException("O arquivo enviado esta vazio.");

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension))
                throw new InvalidOperationException("Formato de anexo nao permitido.");

            var uploadRoot = Path.Combine(_environment.ContentRootPath, "uploads", "solicitacoes");
            Directory.CreateDirectory(uploadRoot);

            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadRoot, fileName);

            await using var stream = System.IO.File.Create(fullPath);
            await file.CopyToAsync(stream);

            return Path.Combine("uploads", "solicitacoes", fileName).Replace('\\', '/');
        }

        private string GetAttachmentPath(string relativePath)
        {
            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, normalized));
        }

        private static string GetContentType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }
    }
}
