using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniFlowHub.Api.Data.Interfaces;

namespace UniFlowHub.Api.Data.Repositories
{
    public class BaseRepo<T> : IBaseRepo<T> where T : class
    {
        protected readonly AppDbContext _context;

        public BaseRepo(AppDbContext context)
        {
            _context = context;
        }

        public IQueryable<T> Query()
        {
            return _context.Set<T>();
        }        

        public void Add(T entity)
        {
            _context.Set<T>().Add(entity);
        }

        public void Update(T entity)
        {
            _context.Set<T>().Update(entity);
        }

        public void Delete(T entity)
        {
            _context.Set<T>().Remove(entity);
        }

        public void Save()
        {
            _context.SaveChanges();
        }

    }
}