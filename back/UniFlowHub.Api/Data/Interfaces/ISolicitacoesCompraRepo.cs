using UniFlowHub.Api.Models;

namespace UniFlowHub.Api.Data.Interfaces
{
    public interface ISolicitacoesCompraRepo : IBaseRepo<SolicitacaoCompra>
    {
        IQueryable<SolicitacaoCompraComunicacao> QueryComunicacoes();
        void AddComunicacao(SolicitacaoCompraComunicacao entity);
    }
}
