using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DRFlowHub.Api.Data.Interfaces;
using DRFlowHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DRFlowHub.Api.Data.Repositories
{
    public class UserRepo : BaseRepo<Users>, IUserRepo
    {
        public UserRepo(AppDbContext context) : base (context) {}

        public Users? GetByLogin(string email)
        {
            return _context.User
                                .FirstOrDefault(u => u.Email == email);
        }

        public bool HasAnyUser()
        {
            return _context.User.Any();
        }
    }
}
