using UniFlowHub.Api.Data.Interfaces;
using UniFlowHub.Api.Models;

namespace UniFlowHub.Api.Data.Repositories
{
    public class ChamadosTIRepo : BaseRepo<ChamadosTI>, IChamadosTIRepo
    {
        public ChamadosTIRepo(AppDbContext context) : base(context) { }

        public IQueryable<ChamadoTIComunicacao> QueryComunicacoes()
        {
            return _context.ChamadoTIComunicacao;
        }

        public void AddComunicacao(ChamadoTIComunicacao entity)
        {
            _context.ChamadoTIComunicacao.Add(entity);
        }

        public void UpdateComunicacao(ChamadoTIComunicacao entity)
        {
            _context.ChamadoTIComunicacao.Update(entity);
        }
    }
}
