using UniFlowHub.Api.Data.Interfaces;
using UniFlowHub.Api.Models;

namespace UniFlowHub.Api.Data.Repositories
{
    public class SolicitacoesCompraRepo : BaseRepo<SolicitacaoCompra>, ISolicitacoesCompraRepo
    {
        public SolicitacoesCompraRepo(AppDbContext context) : base(context) { }

        public IQueryable<SolicitacaoCompraComunicacao> QueryComunicacoes()
        {
            return _context.SolicitacaoCompraComunicacao;
        }

        public void AddComunicacao(SolicitacaoCompraComunicacao entity)
        {
            _context.SolicitacaoCompraComunicacao.Add(entity);
        }
    }
}
