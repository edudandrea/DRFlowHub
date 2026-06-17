using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/monitoramento")]
    public class MonitoramentoController : ControllerBase
    {
        private static readonly int[] DefaultPorts = [443, 80, 8443, 8080];

        [HttpPost("testar")]
        public async Task<IActionResult> Testar([FromBody] MonitoramentoTesteRequest request, CancellationToken cancellationToken)
        {
            if (!CanMonitor())
                return Forbid();

            if (string.IsNullOrWhiteSpace(request.Alvo))
                return BadRequest("Informe o IP, host ou URL para testar.");

            var sw = Stopwatch.StartNew();
            try
            {
                var result = await ProbeAsync(request.Alvo.Trim(), cancellationToken);
                sw.Stop();
                return Ok(new MonitoramentoTesteResponse(true, result.Status, result.Mensagem, sw.ElapsedMilliseconds, result.Protocolo));
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or SocketException or HttpRequestException or PingException)
            {
                sw.Stop();
                return Ok(new MonitoramentoTesteResponse(false, "offline", ex.Message, sw.ElapsedMilliseconds, "falha"));
            }
        }

        private bool CanMonitor()
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "TI", StringComparison.OrdinalIgnoreCase)
                || User.HasClaim("access", "ti-admin");
        }

        private static async Task<ProbeResult> ProbeAsync(string target, CancellationToken cancellationToken)
        {
            if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                return await ProbeUriAsync(uri, cancellationToken);
            }

            var host = target;
            var port = 0;
            if (TrySplitHostPort(target, out var parsedHost, out var parsedPort))
            {
                host = parsedHost;
                port = parsedPort;
            }

            var ping = await TryPingAsync(host, cancellationToken);
            if (ping is not null)
                return ping;

            var ports = port > 0 ? [port] : DefaultPorts;
            foreach (var candidatePort in ports)
            {
                var tcp = await TryTcpAsync(host, candidatePort, cancellationToken);
                if (tcp is not null)
                    return tcp;
            }

            throw new TimeoutException($"Sem resposta de {host} por ping ou portas {string.Join(", ", ports)}.");
        }

        private static async Task<ProbeResult> ProbeUriAsync(Uri uri, CancellationToken cancellationToken)
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return new ProbeResult("online", $"Resposta HTTP {(int)response.StatusCode} em {uri.Host}.", "http");
        }

        private static async Task<ProbeResult?> TryPingAsync(string host, CancellationToken cancellationToken)
        {
            try
            {
                using var ping = new Ping();
                var pingTask = ping.SendPingAsync(host, 3000);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                var completed = await Task.WhenAny(pingTask, timeoutTask);
                if (completed != pingTask)
                    return null;

                var reply = await pingTask;
                return reply.Status == IPStatus.Success
                    ? new ProbeResult("online", $"Ping respondido por {host}.", "icmp")
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<ProbeResult?> TryTcpAsync(string host, int port, CancellationToken cancellationToken)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port, cancellationToken).AsTask();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                var completed = await Task.WhenAny(connectTask, timeoutTask);
                if (completed != connectTask || !client.Connected)
                    return null;

                return new ProbeResult("online", $"Porta TCP {port} respondeu em {host}.", "tcp");
            }
            catch
            {
                return null;
            }
        }

        private static bool TrySplitHostPort(string target, out string host, out int port)
        {
            host = target;
            port = 0;

            var lastColon = target.LastIndexOf(':');
            if (lastColon <= 0 || lastColon == target.Length - 1)
                return false;

            var portText = target[(lastColon + 1)..];
            if (!int.TryParse(portText, out var parsedPort) || parsedPort <= 0 || parsedPort > 65535)
                return false;

            host = target[..lastColon].Trim('[', ']');
            port = parsedPort;
            return !string.IsNullOrWhiteSpace(host);
        }

        private sealed record ProbeResult(string Status, string Mensagem, string Protocolo);
    }

    public sealed record MonitoramentoTesteRequest(string Alvo);
    public sealed record MonitoramentoTesteResponse(bool Online, string Status, string Mensagem, long TempoRespostaMs, string Protocolo);
}
