using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/ti-assistant")]
    public class TiAssistantController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public TiAssistantController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("global-search")]
        public async Task<IActionResult> GlobalSearch([FromBody] TiAssistantGlobalSearchRequest request, CancellationToken cancellationToken)
        {
            if (!CanUseAssistant())
                return Forbid();

            if (string.IsNullOrWhiteSpace(request.Question) && string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Informe uma pergunta ou titulo para o TI Assistant.");

            var apiKey = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Ok(new TiAssistantGlobalSearchResponse(false, string.Empty, "OpenAI nao configurado no servidor."));
            }

            var model = _configuration["OpenAI:TiAssistantModel"] ?? _configuration["OpenAI:Model"] ?? "gpt-4.1-mini";
            var prompt = BuildPrompt(request);
            using var http = _httpClientFactory.CreateClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model,
                input = prompt,
                temperature = 0.2,
                max_output_tokens = 700
            }), Encoding.UTF8, "application/json");

            using var response = await http.SendAsync(httpRequest, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, new TiAssistantGlobalSearchResponse(true, string.Empty, raw));
            }

            return Ok(new TiAssistantGlobalSearchResponse(true, ExtractText(raw), string.Empty));
        }

        private bool CanUseAssistant()
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "TI", StringComparison.OrdinalIgnoreCase)
                || User.HasClaim("access", "ti-admin")
                || User.Identity?.IsAuthenticated == true;
        }

        private static string BuildPrompt(TiAssistantGlobalSearchRequest request)
        {
            return $"""
            Voce e o T.I Assistant do UniFlowHub. Ajude o usuario a diagnosticar um problema de TI antes da abertura do chamado.
            Responda em portugues do Brasil, com passos praticos, seguros e objetivos.
            Nao invente dados internos. Se precisar de informacao da empresa, diga exatamente o que o usuario deve informar no chamado.

            Titulo do chamado: {request.Title}
            Pergunta do usuario: {request.Question}
            Contexto local da base de conhecimento: {request.LocalContext}
            """;
        }

        private static string ExtractText(string raw)
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.TryGetProperty("output_text", out var outputText))
                return outputText.GetString() ?? string.Empty;

            if (!document.RootElement.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
                return string.Empty;

            var builder = new StringBuilder();
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text))
                        builder.AppendLine(text.GetString());
                }
            }

            return builder.ToString().Trim();
        }
    }

    public sealed record TiAssistantGlobalSearchRequest(string Title, string Question, string LocalContext);
    public sealed record TiAssistantGlobalSearchResponse(bool Configured, string Answer, string Error);
}
