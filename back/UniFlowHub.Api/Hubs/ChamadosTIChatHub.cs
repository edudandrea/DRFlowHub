using System.Security.Claims;
using UniFlowHub.Api.Dtos.ChamadosTI;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace UniFlowHub.Api.Hubs
{
    [Authorize]
    public class ChamadosTIChatHub : Hub
    {
        private readonly ChamadosTIService _service;

        public ChamadosTIChatHub(ChamadosTIService service)
        {
            _service = service;
        }

        public static string GroupName(int chamadoId) => $"chamado-ti-{chamadoId}";

        public async Task EntrarNoChamado(int chamadoId)
        {
            _service.EnsureCanAccess(chamadoId, GetRole(), GetUserId(), GetAcessos());
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(chamadoId));
        }

        public async Task SairDoChamado(int chamadoId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(chamadoId));
        }

        public async Task EnviarMensagem(int chamadoId, string mensagem)
        {
            var comunicacao = _service.AddComunicacao(
                chamadoId,
                new ChamadoTIComunicacaoCreateDto { Mensagem = mensagem },
                GetRole(),
                GetUserId(),
                GetAcessos());

            await Clients.Group(GroupName(chamadoId)).SendAsync("MensagemRecebida", comunicacao);
            var ownerUserId = _service.GetOwnerUserId(chamadoId);
            if (ownerUserId > 0 && ownerUserId != comunicacao.AutorUserId)
                await Clients.User(ownerUserId.ToString()).SendAsync("NovaMensagemChamado", comunicacao);
        }

        private int GetUserId()
        {
            var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(value, out var userId))
                return userId;

            throw new HubException("Usuario invalido.");
        }

        private string GetRole()
        {
            return Context.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        }

        private IEnumerable<string> GetAcessos()
        {
            return Context.User?.FindAll("access").Select(claim => claim.Value) ?? Enumerable.Empty<string>();
        }
    }
}
