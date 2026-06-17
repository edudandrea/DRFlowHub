using System.Security.Claims;
using UniFlowHub.Api.Dtos.EquipamentosTI;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class EquipamentosTIController : ControllerBase
    {
        private readonly EquipamentosTIService _service;
        private readonly IWebHostEnvironment _environment;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        public EquipamentosTIController(EquipamentosTIService service, IWebHostEnvironment environment)
        {
            _service = service;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult List()
        {
            return Ok(_service.List(GetRole()));
        }

        [HttpPost]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Add([FromForm] EquipamentoTICreateDto dto)
        {
            try
            {
                var documentoUrl = dto.Documento is null ? string.Empty : await SaveAttachment(dto.Documento);
                var equipamento = _service.Add(dto, GetRole(), GetUserId(), documentoUrl);

                return Ok(new { sucesso = true, mensagem = "Movimentacao registrada com sucesso", equipamento });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        public IActionResult Update(int id, [FromBody] EquipamentoTIUpdateDto dto)
        {
            try
            {
                var equipamento = _service.Update(id, dto, GetRole());
                return Ok(new { sucesso = true, mensagem = "Movimentacao atualizada com sucesso", equipamento });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id, [FromBody] EquipamentoTIExcluirDto dto)
        {
            try
            {
                _service.Excluir(id, dto, GetRole(), GetUserId());
                return Ok(new { sucesso = true, mensagem = "Inventario excluido com sucesso" });
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

        [HttpGet("{id:int}/documento")]
        public IActionResult DownloadAttachment(int id)
        {
            var equipamento = _service.GetAttachmentOwner(id, GetRole());
            var fullPath = GetAttachmentPath(equipamento.DocumentoUrl);

            if (!System.IO.File.Exists(fullPath))
                return NotFound("Documento nao encontrado.");

            return PhysicalFile(fullPath, GetContentType(Path.GetExtension(fullPath)), Path.GetFileName(fullPath), enableRangeProcessing: true);
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

        private async Task<string> SaveAttachment(IFormFile file)
        {
            if (file.Length == 0)
                throw new InvalidOperationException("O arquivo enviado esta vazio.");

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension))
                throw new InvalidOperationException("Formato de documento nao permitido.");

            var uploadRoot = Path.Combine(_environment.ContentRootPath, "uploads", "equipamentos-ti");
            Directory.CreateDirectory(uploadRoot);

            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadRoot, fileName);

            await using var stream = System.IO.File.Create(fullPath);
            await file.CopyToAsync(stream);

            return Path.Combine("uploads", "equipamentos-ti", fileName).Replace('\\', '/');
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
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };
        }
    }
}
