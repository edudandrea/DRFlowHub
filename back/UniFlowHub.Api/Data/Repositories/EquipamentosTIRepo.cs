using UniFlowHub.Api.Data.Interfaces;
using UniFlowHub.Api.Models;

namespace UniFlowHub.Api.Data.Repositories
{
    public class EquipamentosTIRepo : BaseRepo<EquipamentoTI>, IEquipamentosTIRepo
    {
        public EquipamentosTIRepo(AppDbContext context) : base(context) { }
    }
}
