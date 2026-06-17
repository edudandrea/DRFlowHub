using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniFlowHub.Api.Data.Repositories;
using UniFlowHub.Api.Models;

namespace UniFlowHub.Api.Data.Interfaces
{
    public interface ISolicitacoesRHRepo : IBaseRepo<SolicitacoesRH>
    {
        IQueryable<SolicitacaoRHComunicacao> QueryComunicacoes();
        void AddComunicacao(SolicitacaoRHComunicacao entity);
    }
}
