using System.Security.Claims;
using UniFlowHub.Api.Dtos.ChamadosTI;
using UniFlowHub.Api.Hubs;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ChamadosTIController : ControllerBase
    {
        private readonly ChamadosTIService _service;
        private readonly IWebHostEnvironment _environment;
        private readonly IHubContext<ChamadosTIChatHub> _chatHub;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".webp",
            ".pdf",
            ".doc",
            ".docx"
        };

        public ChamadosTIController(
            ChamadosTIService service,
            IWebHostEnvironment environment,
            IHubContext<ChamadosTIChatHub> chatHub)
        {
            _service = service;
            _environment = environment;
            _chatHub = chatHub;
        }

        [HttpGet]
        public IActionResult List()
        {
            return Ok(_service.List(GetRole(), GetUserId(), GetAcessos()));
        }

        [HttpPost]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Add([FromForm] ChamadoTICreateDto dto)
        {
            try
            {
                var anexo = dto.Anexo ?? dto.Imagem;
                var anexoUrl = anexo is null ? string.Empty : await SaveAttachment(anexo);
                dto.EquipamentoIp = GetClientIp();
                var chamado = _service.Add(dto, GetRole(), GetUserId(), anexoUrl, GetAcessos());

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "Chamado criado com sucesso",
                    chamado
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        public IActionResult Update(int id, [FromBody] ChamadoTIUpdateDto dto)
        {
            try
            {
                var chamado = _service.Update(id, dto, GetRole(), GetUserId(), GetAcessos());

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "Chamado atualizado com sucesso",
                    chamado
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
        public async Task<IActionResult> AddComunicacao(int id, [FromBody] ChamadoTIComunicacaoCreateDto dto)
        {
            try
            {
                var comunicacao = _service.AddComunicacao(id, dto, GetRole(), GetUserId(), GetAcessos());
                await _chatHub.Clients.Group(ChamadosTIChatHub.GroupName(id)).SendAsync("MensagemRecebida", comunicacao);
                var ownerUserId = _service.GetOwnerUserId(id);
                if (ownerUserId > 0 && ownerUserId != comunicacao.AutorUserId)
                    await _chatHub.Clients.User(ownerUserId.ToString()).SendAsync("NovaMensagemChamado", comunicacao);

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

        [HttpGet("{id:int}/comunicacoes/download")]
        public IActionResult DownloadComunicacoes(int id)
        {
            try
            {
                var content = _service.BuildComunicacoesDownload(id, GetRole(), GetUserId(), GetAcessos());
                var bytes = Encoding.UTF8.GetBytes(content);
                return File(bytes, "text/plain; charset=utf-8", $"chamado-{id}-historico-chat.txt");
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
        }

        [HttpPost("{id:int}/comunicacoes/{comunicacaoId:int}/lida")]
        public async Task<IActionResult> MarcarComunicacaoLida(int id, int comunicacaoId)
        {
            try
            {
                var comunicacao = _service.MarcarComunicacaoLida(id, comunicacaoId, GetRole(), GetUserId(), GetAcessos());
                await _chatHub.Clients.Group(ChamadosTIChatHub.GroupName(id)).SendAsync("MensagemLida", comunicacao);

                return Ok(comunicacao);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPost("{id:int}/encerrar")]
        public IActionResult Encerrar(int id, [FromBody] ChamadoTIEncerrarDto dto)
        {
            try
            {
                var chamado = _service.Encerrar(id, dto, GetRole(), GetUserId(), GetAcessos());

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "Chamado encerrado com sucesso",
                    chamado
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id:int}/satisfacao")]
        public IActionResult AvaliarSatisfacao(int id, [FromBody] ChamadoTISatisfacaoDto dto)
        {
            try
            {
                var chamado = _service.AvaliarSatisfacao(id, dto, GetRole(), GetUserId(), GetAcessos());

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "Avaliacao registrada com sucesso",
                    chamado
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id:int}/aprovacao")]
        public IActionResult Aprovar(int id, [FromBody] ChamadoTIAprovacaoDto dto)
        {
            try
            {
                var chamado = _service.Aprovar(id, dto, GetRole(), GetUserId(), GetAcessos());

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "Aprovacao registrada com sucesso",
                    chamado
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
            try
            {
                var chamado = _service.Reabrir(id, GetRole(), GetUserId(), GetAcessos());

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "Chamado reaberto com sucesso",
                    chamado
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id:int}/imagem")]
        public IActionResult DownloadImage(int id)
        {
            return DownloadAttachment(id);
        }

        [HttpGet("{id:int}/anexo")]
        public IActionResult DownloadAttachment(int id)
        {
            var chamado = _service.GetAttachmentOwner(id, GetRole(), GetUserId(), GetAcessos());
            var fullPath = GetAttachmentPath(chamado.AnexoImagemUrl);

            if (!System.IO.File.Exists(fullPath))
                return NotFound("Anexo nao encontrado.");

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

        private string GetClientIp()
        {
            var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
                return forwardedFor.Split(',')[0].Trim();

            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        }

        private async Task<string> SaveAttachment(IFormFile file)
        {
            if (file.Length == 0)
                throw new InvalidOperationException("O arquivo enviado esta vazio.");

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension))
                throw new InvalidOperationException("Envie apenas PDF, DOC, DOCX ou imagens JPG, PNG, GIF e WEBP.");

            var uploadRoot = Path.Combine(_environment.ContentRootPath, "uploads", "ti");
            Directory.CreateDirectory(uploadRoot);

            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadRoot, fileName);

            await using var stream = System.IO.File.Create(fullPath);
            await file.CopyToAsync(stream);

            return Path.Combine("uploads", "ti", fileName).Replace('\\', '/');
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
