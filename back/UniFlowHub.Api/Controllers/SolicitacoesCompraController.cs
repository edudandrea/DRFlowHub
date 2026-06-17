using System.Security.Claims;
using UniFlowHub.Api.Dtos.SolicitacoesCompra;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class SolicitacoesCompraController : ControllerBase
    {
        private readonly SolicitacoesCompraService _service;
        private readonly IWebHostEnvironment _environment;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".xls", ".xlsx"
        };

        public SolicitacoesCompraController(SolicitacoesCompraService service, IWebHostEnvironment environment)
        {
            _service = service;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult List()
        {
            return Ok(_service.List(GetRole(), GetUserId(), GetAcessos()));
        }

        [HttpPost]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Add([FromForm] SolicitacaoCompraCreateDto dto)
        {
            try
            {
                var documentoUrl = dto.Documento is null ? string.Empty : await SaveAttachment(dto.Documento);
                var solicitacao = _service.Add(dto, GetRole(), GetUserId(), documentoUrl, GetAcessos());

                return Ok(new { sucesso = true, mensagem = "Solicitacao de compra criada com sucesso", solicitacao });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        public IActionResult Update(int id, [FromBody] SolicitacaoCompraUpdateDto dto)
        {
            try
            {
                var solicitacao = _service.Update(id, dto, GetRole(), GetUserId(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Solicitacao de compra atualizada com sucesso", solicitacao });
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
        public IActionResult AddComunicacao(int id, [FromBody] SolicitacaoCompraComunicacaoCreateDto dto)
        {
            try
            {
                var comunicacao = _service.AddComunicacao(id, dto, GetRole(), GetUserId(), GetAcessos());
                return Ok(new { sucesso = true, mensagem = "Mensagem enviada com sucesso", comunicacao });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id:int}/aprovacao")]
        public IActionResult Aprovar(int id, [FromBody] SolicitacaoCompraAprovacaoDto dto)
        {
            var solicitacao = _service.Aprovar(id, dto, GetRole(), GetUserId(), GetAcessos());
            return Ok(new { sucesso = true, mensagem = "Aprovacao registrada com sucesso", solicitacao });
        }

        [HttpGet("{id:int}/documento")]
        public IActionResult DownloadAttachment(int id)
        {
            var solicitacao = _service.GetAttachmentOwner(id, GetRole(), GetUserId(), GetAcessos());
            var fullPath = GetAttachmentPath(solicitacao.DocumentoUrl);

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
                throw new InvalidOperationException("Formato de documento nao permitido.");

            var uploadRoot = Path.Combine(_environment.ContentRootPath, "uploads", "compras");
            Directory.CreateDirectory(uploadRoot);

            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadRoot, fileName);

            await using var stream = System.IO.File.Create(fullPath);
            await file.CopyToAsync(stream);

            return Path.Combine("uploads", "compras", fileName).Replace('\\', '/');
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
                _ => "application/octet-stream"
            };
        }
    }
}
