using System.Security.Claims;
using UniFlowHub.Api.Dtos.BaseConhecimentoTI;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/base-conhecimento-ti")]
    public class BaseConhecimentoTIController : ControllerBase
    {
        private readonly BaseConhecimentoTIService _service;
        private readonly IWebHostEnvironment _environment;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        public BaseConhecimentoTIController(BaseConhecimentoTIService service, IWebHostEnvironment environment)
        {
            _service = service;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            return Ok(await _service.ListAsync(GetRole(), GetAcessos()));
        }

        [HttpPost]
        [RequestSizeLimit(25 * 1024 * 1024)]
        public async Task<IActionResult> Add([FromForm] BaseConhecimentoTICreateDto dto)
        {
            try
            {
                var saved = dto.Documento is null
                    ? new SavedFile(string.Empty, string.Empty, string.Empty)
                    : await SaveAttachment(dto.Documento);

                var item = await _service.AddAsync(dto, GetRole(), GetUserId(), GetAcessos(), saved.Url, saved.OriginalName, saved.ContentType);
                return Ok(new { sucesso = true, mensagem = "Conhecimento cadastrado com sucesso", conhecimento = item });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        [RequestSizeLimit(25 * 1024 * 1024)]
        public async Task<IActionResult> Update(int id, [FromForm] BaseConhecimentoTIUpdateDto dto)
        {
            try
            {
                var saved = dto.Documento is null ? null : await SaveAttachment(dto.Documento);
                var item = await _service.UpdateAsync(
                    id,
                    dto,
                    GetRole(),
                    GetAcessos(),
                    saved?.Url,
                    saved?.OriginalName,
                    saved?.ContentType);

                return Ok(new { sucesso = true, mensagem = "Conhecimento atualizado com sucesso", conhecimento = item });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id, GetRole(), GetAcessos());
            return Ok(new { sucesso = true, mensagem = "Conhecimento removido com sucesso" });
        }

        [HttpGet("{id:int}/documento")]
        public async Task<IActionResult> DownloadAttachment(int id)
        {
            var item = await _service.GetAttachmentOwnerAsync(id, GetRole(), GetAcessos());
            var fullPath = GetAttachmentPath(item.ArquivoUrl);

            if (!System.IO.File.Exists(fullPath))
                return NotFound("Documento nao encontrado.");

            return PhysicalFile(fullPath, item.ArquivoContentType, item.ArquivoNome, enableRangeProcessing: true);
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

        private async Task<SavedFile> SaveAttachment(IFormFile file)
        {
            if (file.Length == 0)
                throw new InvalidOperationException("O arquivo enviado esta vazio.");

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension))
                throw new InvalidOperationException("Formato de documento nao permitido.");

            var uploadRoot = Path.Combine(_environment.ContentRootPath, "uploads", "base-conhecimento-ti");
            Directory.CreateDirectory(uploadRoot);

            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadRoot, fileName);

            await using var stream = System.IO.File.Create(fullPath);
            await file.CopyToAsync(stream);

            var url = Path.Combine("uploads", "base-conhecimento-ti", fileName).Replace('\\', '/');
            return new SavedFile(url, Path.GetFileName(file.FileName), GetContentType(extension));
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
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }

        private sealed record SavedFile(string Url, string OriginalName, string ContentType);
    }
}
