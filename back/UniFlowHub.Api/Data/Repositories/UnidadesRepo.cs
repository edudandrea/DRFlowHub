using UniFlowHub.Api.Data.Interfaces;
using UniFlowHub.Api.Models;

namespace UniFlowHub.Api.Data.Repositories
{
    public class UnidadesRepo : BaseRepo<Unidade>, IUnidadesRepo
    {
        public UnidadesRepo(AppDbContext context) : base(context)
        {
        }
    }
}
