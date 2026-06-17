using UniFlowHub.Api.Models;

namespace UniFlowHub.Api.Data.Interfaces
{
    public interface IChamadosTIRepo : IBaseRepo<ChamadosTI>
    {
        IQueryable<ChamadoTIComunicacao> QueryComunicacoes();
        void AddComunicacao(ChamadoTIComunicacao entity);
        void UpdateComunicacao(ChamadoTIComunicacao entity);
    }
}
