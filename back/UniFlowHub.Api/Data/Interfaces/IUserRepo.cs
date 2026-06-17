using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniFlowHub.Api.Data.Repositories;
using UniFlowHub.Api.Models;

namespace UniFlowHub.Api.Data.Interfaces
{
    public interface IUserRepo : IBaseRepo<Users>
    {
         Users? GetByLogin(string login);
         bool HasAnyUser();
    }
}