using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniFlowHub.Api.Data.Interfaces;
using UniFlowHub.Api.Models;

namespace UniFlowHub.Api.Data.Repositories
{
    public class SolicitacoesRHRepo : BaseRepo<SolicitacoesRH> , ISolicitacoesRHRepo
    {
        public SolicitacoesRHRepo(AppDbContext context) : base (context){ }

        public IQueryable<SolicitacaoRHComunicacao> QueryComunicacoes()
        {
            return _context.SolicitacaoRHComunicacao;
        }

        public void AddComunicacao(SolicitacaoRHComunicacao entity)
        {
            _context.SolicitacaoRHComunicacao.Add(entity);
        }
    }
}
